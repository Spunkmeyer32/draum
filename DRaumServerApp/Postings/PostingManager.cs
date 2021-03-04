using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace DRaumServerApp.Postings
{
  internal class PostingManager
  {
    [JsonIgnore]
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    [JsonIgnore]
    private readonly PostingPublishManager publishManager = new PostingPublishManager();
    [JsonIgnore]
    private volatile bool postsCheckChangeFlag = true;
    [JsonIgnore] 
    private readonly object lastPostingIdMutex = new object();

    [JsonProperty]
    private long lastPostingId;
    [JsonProperty]
    private ConcurrentDictionary<long, Posting> postings;
    [JsonProperty]
    private ConcurrentQueue<Posting> postingsToCheck;
    [JsonProperty]
    private ConcurrentDictionary<long, Posting> postingsInCheck;
    [JsonProperty]
    private ConcurrentQueue<long> postingsToPublish;
    [JsonProperty]
    private ConcurrentQueue<long> postingsToPublishHappyHour;
    [JsonProperty]
    private ConcurrentQueue<long> postingsToPublishPremiumHour;

    internal PostingManager()
    {
      if (Utilities.Runningintestmode)
      {
        Posting.Daysuntildeletenormal = 2;
      }
      lock (this.lastPostingIdMutex)
      {
        this.lastPostingId = 1;
      }
      this.postings = new ConcurrentDictionary<long, Posting>();
      this.postingsToCheck = new ConcurrentQueue<Posting>();
      this.postingsInCheck = new ConcurrentDictionary<long, Posting>();
      this.postingsToPublish = new ConcurrentQueue<long>();
      this.postingsToPublishHappyHour = new ConcurrentQueue<long>();
      this.postingsToPublishPremiumHour = new ConcurrentQueue<long>();
      this.publishManager.calcNextSlot();
    }

    private Posting getPosting(long postingId)
    {
      if (!this.postings.ContainsKey(postingId))
      {
        return null;
      }
      return this.postings.TryGetValue(postingId, out Posting posting) ? posting : null;
    }

    private Posting getPostingInCheck(long postingId)
    {
      if (!this.postingsInCheck.ContainsKey(postingId))
      {
        return null;
      }
      return this.postingsInCheck.TryGetValue(postingId, out Posting posting) ? posting : null;
    }

    internal void transferFromInCheckToToCheck()
    {
      foreach (KeyValuePair<long, Posting> kvp in this.postingsInCheck)
      {
        this.postingsToCheck.Enqueue(kvp.Value);
        this.postsCheckChangeFlag = true;
      }
      this.postingsInCheck.Clear();
    }

    internal void addPosting(string text, long authorId)
    {
      lock (this.lastPostingIdMutex)
      {
        Posting posting = new Posting(this.lastPostingId++, text, authorId);
        this.postingsToCheck.Enqueue(posting);
      }
      this.postsCheckChangeFlag = true;
    }

    internal long tryPublish()
    {
      // Welche Stunde haben wir?
      long postId = 0;
      try
      {
        switch (this.publishManager.getCurrentpublishType())
        {
          case PostingPublishManager.PublishHourType.Normal:
            if (!this.postingsToPublish.TryDequeue(out postId))
            {
              postId = 0;
            }
            break;
          case PostingPublishManager.PublishHourType.Happy:
            if (!this.postingsToPublishHappyHour.TryDequeue(out postId))
            {
              postId = 0;
            }
            break;
          case PostingPublishManager.PublishHourType.Premium:
            if (!this.postingsToPublishPremiumHour.TryDequeue(out postId))
            {
              postId = 0;
            }
            break;
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Posting konnte nicht veröffentlicht werden, postId war: " + postId);
        postId = 0;
      }
      return postId;
    }

    internal void setPublishTimestamp(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.getPosting(postingId)?.setPublishTimestamp(new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0));
      }
    }

    public void unsetPublishTimestamp(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.getPosting(postingId)?.setPublishTimestamp(new DateTime(1999, 1, 1));
      }
    }

    internal void testPublishing(long postingId, DateTime dateTime)
    {
      // Welche Stunde haben wir?
      this.getPosting(postingId)?.setPublishTimestamp(dateTime);
    }

    internal void reAcceptFailedPost(long postingId)
    {
      DateTime nextFreeSlotPremium = this.publishManager.getTimestampOfNextSlot(this.postingsToPublishPremiumHour.Count, PostingPublishManager.PublishHourType.Premium);
      DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.PublishHourType.Normal);
      DateTime nextFreeSlotHappy = this.publishManager.getTimestampOfNextSlot(this.postingsToPublishHappyHour.Count, PostingPublishManager.PublishHourType.Happy);
      if(nextFreeSlotPremium < nextFreeSlotNormal)
      {
        if (nextFreeSlotHappy < nextFreeSlotPremium)
        {
          this.postingsToPublishHappyHour.Enqueue(postingId);
        }
        else
        {
          this.postingsToPublishPremiumHour.Enqueue(postingId);
        }
      }
      else
      {
        if (nextFreeSlotHappy < nextFreeSlotNormal)
        {
          this.postingsToPublishHappyHour.Enqueue(postingId);
        }
        else
        {
          this.postingsToPublish.Enqueue(postingId);
        }
      }
    }

    /// <summary>
    /// Ein Beitrag wurde durch die Moderation angenommen. Es wird anhand das Veröffentlichungstyps
    /// die Reihe gewählt, in der die schnellste Veröffentlichung möglich ist.
    /// </summary>
    /// <param name="postingId">ID des angenommenen Beitrags</param>
    /// <param name="publishType">Höchstwertiger Veröffentlichungstyp</param>
    /// <returns>"" im Fehlerfall, ansonsten die Erfolgsmeldung für den Autor/Moderator</returns>
    [NotNull]
    internal string acceptPost(long postingId, PostingPublishManager.PublishHourType publishType)
    {
      if (!this.postingsInCheck.ContainsKey(postingId))
      {
        return "";
      }
      Posting postingToAccept = this.postingsInCheck[postingId];
      // Aus der einen Liste entfernen und in die Andere transferieren
      if (!this.postingsInCheck.TryRemove(postingToAccept.getPostId(), out _))
      {
        return "";
      }
      this.postings.TryAdd(postingToAccept.getPostId(),postingToAccept);
      if(publishType == PostingPublishManager.PublishHourType.Premium)
      {
        DateTime nextFreeSlotPremium = this.publishManager.getTimestampOfNextSlot(this.postingsToPublishPremiumHour.Count, PostingPublishManager.PublishHourType.Premium);
        DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.PublishHourType.Normal);
        if(nextFreeSlotPremium < nextFreeSlotNormal)
        {
          this.postingsToPublishPremiumHour.Enqueue(postingToAccept.getPostId());
          return "Veröffentlichung voraussichtlich: " + nextFreeSlotPremium.ToString(Utilities.UsedCultureInfo);
        }
        else
        {
          this.postingsToPublish.Enqueue(postingToAccept.getPostId());
          return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString(Utilities.UsedCultureInfo);
        }          
      }
      else
      {
        if(publishType == PostingPublishManager.PublishHourType.Happy)
        {
          DateTime nextFreeSlotHappy = this.publishManager.getTimestampOfNextSlot(this.postingsToPublishHappyHour.Count, PostingPublishManager.PublishHourType.Happy);
          DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.PublishHourType.Normal);
          if (nextFreeSlotHappy < nextFreeSlotNormal)
          {
            this.postingsToPublishHappyHour.Enqueue(postingToAccept.getPostId());
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotHappy.ToString(Utilities.UsedCultureInfo);
          }
          else
          {
            this.postingsToPublish.Enqueue(postingToAccept.getPostId());
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString(Utilities.UsedCultureInfo);
          }
        }
        else
        {
          DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.PublishHourType.Normal);
          this.postingsToPublish.Enqueue(postingToAccept.getPostId());
          return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString(Utilities.UsedCultureInfo);
        }
      }
    }

    

    internal int howManyPostsToCheck()
    {
        return this.postingsToCheck.Count;
    }

    internal bool getAndResetPostsCheckChangeFlag()
    {
      if(this.postsCheckChangeFlag)
      {
        this.postsCheckChangeFlag = false;
        return true;
      }
      return false;
    }

    internal void resetDirtyFlag(long postingId)
    {
      this.getPosting(postingId)?.resetDirtyFlag();
    }

    internal void resetTextDirtyFlag(long postingId)
    {
      this.getPosting(postingId)?.resetTextDirtyFlag();
    }


    internal IEnumerable<long> getDirtyPosts()
    {
      List<long> postlist = new List<long>();
      foreach (Posting posting in this.postings.Values)
      {
        if (posting.isDirty())
        {
          postlist.Add(posting.getPostId());
        }
      }
      return postlist;
    }

    internal IEnumerable<long> getTextDirtyPosts()
    {
      List<long> postlist = new List<long>();
      try
      {
        foreach (Posting posting in this.postings.Values)
        {
          if (posting.isTextDirty())
          {
            postlist.Add(posting.getPostId());
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Anlegen der Liste geänderter Posts");
      }
      return postlist;
    }

    internal IEnumerable<long> getFlaggedPosts()
    {
      List<long> postlist = new List<long>();
      try
      {
        foreach (Posting posting in this.postings.Values)
        {
          if (posting.isFlagged())
          {
            postlist.Add(posting.getPostId());
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Anlegen der Liste geflaggter Posts");
      }
      return postlist;
    }

    internal KeyValuePair<long, string> getNextPostToCheck()
    {
      if (!this.postingsToCheck.TryDequeue(out var post))
      {
        return new KeyValuePair<long, string>(-1, "");
      }
      this.postsCheckChangeFlag = true;
      if (this.postingsInCheck.TryAdd(post.getPostId(), post))
      {
        return new KeyValuePair<long, string>(post.getPostId(), post.getPostingText());
      }
      else
      {
        logger.Error("Konnte den Beitrag nicht zu den zur-zeit-geprüften Beiträgen hinzufügen (wird wieder eingefügt) , ID: " + post.getPostId());
        this.postingsToCheck.Enqueue(post);
        this.postsCheckChangeFlag = true;
      }
      return new KeyValuePair<long, string>(-1, "");
    }

    internal bool putBackIntoQueue(long postingId)
    {
      if (this.postingsInCheck.TryRemove(postingId, out var posting))
      {
        // put back
        this.postingsToCheck.Enqueue(posting);
        this.postsCheckChangeFlag = true;
        return true;
      }
      else
      {
        logger.Error("Posting nicht in der Liste In-Check gefunden: " + postingId);
      }
      return false;
    }

    internal Posting removePostFromInCheck(long postId)
    {
      if(!this.postingsInCheck.TryRemove(postId, out var posting))
      {
        logger.Error("Konnte den Post nicht aus der Liste InCheck löschen: " + postId);
        return null;
      }
      return posting;
    }

    internal string getPostingTeaser(long postingId)
    {
      Posting posting = getPosting(postingId);
      if(posting!=null)
      {
        return posting.getPostingText().Substring(0, Math.Min(60, posting.getPostingText().Length)) + " [...]";
      }
      posting = getPostingInCheck(postingId);
      if(posting != null)
      {
        return posting.getPostingText().Substring(0, Math.Min(60, posting.getPostingText().Length)) + " [...]";
      }
      logger.Error("Konnte den Teaser nicht laden: " + postingId);
      return "... Beitrag nicht gefunden ...";
    }

    internal long getAuthorId(long postingId)
    {
      try
      {
        Posting posting = this.getPosting(postingId);
        if(posting != null)
        {
          return posting.getAuthorId();
        }
        foreach (Posting p in this.postingsToCheck)
        {
          if (p.getPostId() == postingId)
          {
            return p.getAuthorId();
          }
        }
        posting = this.getPostingInCheck(postingId);
        if (posting != null)
        {
          return posting.getAuthorId();
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Konnte den Autor nicht ermitteln: " + ex.Message + " PostingId: " + postingId);
      }
      return -1;
    }

    internal void discardPost(long postingId)
    {
      if (this.postingsInCheck.ContainsKey(postingId))
      {
        if (this.postingsInCheck.TryRemove(postingId, out _))
        {
          return;
        }
      }
      logger.Error("Konnte den Post mit der ID " + postingId + " nicht löschen.");
    }

    internal bool isAuthor(long postingId, long authorId)
    {
      Posting posting = this.getPosting(postingId);
      if (posting != null)
      {
        return posting.getAuthorId() == authorId;
      }
      return false;
    }

    internal void upvote(long postingId, int votecount)
    {
      this.getPosting(postingId)?.voteup(votecount);
    }

    internal void downvote(long postingId, int votecount)
    {
      this.getPosting(postingId)?.votedown(votecount);
    }

    internal void flag(long postingId)
    {
      this.getPosting(postingId)?.flag();
    }

    internal int getMessageId(long postingId)
    {
      Posting posting = this.getPosting(postingId);
      if (posting != null)
      {
        return posting.getChatMessageId();
      }
      return -1;
    }

    internal int getMessageIdDaily(long postingId)
    {
      Posting posting = this.getPosting(postingId);
      if (posting != null)
      {
        return posting.getChatMessageDailyId();
      }
      return -1;
    }

    internal int getMessageIdWeekly(long postingId)
    {
      Posting posting = this.getPosting(postingId);
      if (posting != null)
      {
        return posting.getChatMessageWeeklyId();
      }
      return -1;
    }

    internal bool isTopPost(long postingId)
    {
      Posting posting = this.getPosting(postingId);
      return posting != null && posting.getTopPostStatus();
    }

    internal long getUpVotes(long postingId)
    {
      return this.getPosting(postingId)?.getUpVotes() ?? 0;
    }

    internal long getDownVotes(long postingId)
    {
      return this.getPosting(postingId)?.getDownVotes() ?? 0;
    }

    internal string getPostingText(long postingId)
    {
      return this.getPosting(postingId)?.getPostingText() ?? "";
    }

    internal string getPostingTextFromInCheck(long postingId)
    {
      return this.getPostingInCheck(postingId)?.getPostingText() ?? "";
    }

    internal string getPostingStatisticText(long postingId)
    {
      return this.getPosting(postingId)?.getPostStatisticText() ?? "";
    }

    internal void setDailyChatMsgId(long postingId, int messageId)
    {
      this.getPosting(postingId)?.setChatMessageDailyId(messageId);
    }

    internal void setChatMsgId(long postingId, int messageId)
    {
      this.getPosting(postingId)?.setChatMessageId(messageId);
    }

    internal void setWeeklyChatMsgId(long postingId, int messageId)
    {
      this.getPosting(postingId)?.setChatMessageWeeklyId(messageId);
    }

    internal int getFlagCountOfPost(long postingId)
    {
      return this.getPosting(postingId)?.getFlagCount() ?? -1;
    }

    internal bool removeFlagFromPost(long postingId)
    {
      Posting posting = this.getPosting(postingId);
      if (posting == null)
      {
        return false;
      }
      posting.resetFlagStatus();
      return true;
    }

    internal void updateTopPostStatus(DRaumStatistics statistics)
    {
      if (statistics == null)
      {
        logger.Warn("Statistik-Objekt war null und Postings werden nicht geupdatet");
        return;
      }
      foreach (Posting posting in this.postings.Values)
      {
        posting.setTopPostStatus(statistics.isTopPost(posting.getUpVotes(), posting.getVoteCount()));
      }
    }

    internal IEnumerable<long> getPostsToDelete()
    {
      return (from posting in this.postings.Values where posting.shouldBeDeleted() select posting.getPostId()).ToList();
    }

    internal IEnumerable<long> getDailyTopPostsFromYesterday()
    {
      // Iteriere über alle Posts, filtern nach Gestern, Sortieren nach Votes, Top 3 zurück
      try
      {
        DateTime yesterday = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
        yesterday = yesterday.AddDays(-1.0);
        List<Posting> result = new List<Posting>();
        foreach (Posting posting in this.postings.Values)
        {
          TimeSpan diff = posting.getPublishTimestamp() - yesterday;
          if (diff.TotalHours >= 0.0 && diff.TotalHours <= 24.0)
          {
            if (posting.getUpVotePercentage() > 50)
            {
              result.Add(posting);
            }
          }
        }
        if (result.Count > 3)
        {
          Array targetlist = result.ToArray();
          Array.Sort(targetlist, new PostingVoteComparer());
          List<long> resultList = new List<long>
          {
            ((Posting) targetlist.GetValue(0)).getPostId(),
            ((Posting) targetlist.GetValue(1)).getPostId(),
            ((Posting) targetlist.GetValue(2)).getPostId()
          };
          return resultList;
        }
        else
        {
          return result.Select(posting => posting.getPostId()).ToList();
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Fehler beim Filtern nach den Top-Posts von gestern");
        return new List<long>();
      }
    }


    internal List<long> getWeeklyTopPostsFromLastWeek()
    {
      // Iteriere über alle Posts, filtern nach letzte Woche, Sortieren nach Votes, Top 5 zurück
      try
      {
        DateTime lastWeek = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
        lastWeek = lastWeek.AddDays(-7.0);
        List<Posting> result = new List<Posting>();
        foreach (Posting posting in this.postings.Values)
        {
          TimeSpan diff = posting.getPublishTimestamp() - lastWeek;
          if (diff.TotalDays >= 0.0 && diff.TotalDays <= 7.0)
          {
            if (posting.getUpVotePercentage() > 50)
            {
              result.Add(posting);
            }
          }
        }
        if (result.Count > 5)
        {
          Array targetlist = result.ToArray();
          Array.Sort(targetlist, new PostingVoteComparer());
          List<long> resultList = new List<long>
          {
            ((Posting) targetlist.GetValue(0)).getPostId(),
            ((Posting) targetlist.GetValue(1)).getPostId(),
            ((Posting) targetlist.GetValue(2)).getPostId(),
            ((Posting) targetlist.GetValue(3)).getPostId(),
            ((Posting) targetlist.GetValue(4)).getPostId()
          };
          return resultList;
        }
        else
        {
          return result.Select(posting => posting.getPostId()).ToList();
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Fehler beim Filtern nach den Top-Posts der letzten Woche");
        return new List<long>();
      }
    }

    internal long getMedianVotes()
    {
      List<long> voteCounts = this.postings.Values.Select(posting => posting.getVoteCount()).ToList();
      if (voteCounts.Count <= 1)
      {
        return 5;
      }
      long[] countArray = voteCounts.ToArray();
      Array.Sort(countArray);
      return countArray[countArray.Length / 2];
    }


    internal bool removePost(long postingId)
    {
      return this.postings.ContainsKey(postingId) && this.postings.TryRemove(postingId, out _);
    }


    internal bool isPostingInCheck(long postingId)
    {
      return this.postingsInCheck.ContainsKey(postingId);
    }

    public void updatePostText(long postingId, string postingText, bool dontSetDirtyFlag)
    {
      this.getPostingInCheck(postingId)?.updateText(postingText,dontSetDirtyFlag);
    }
  }


}
