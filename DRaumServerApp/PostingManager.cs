using Newtonsoft.Json;
using NLog.Targets;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DRaumServerApp
{
  public class PostingVoteComparer : IComparer
  {
    public int Compare(object x, object y)
    {
      if (x == null && y == null)
      {
        return 0;
      }
      if (x == null)
      {
        return -1;
      }
      if (y == null)
      {
        return 1;
      }
      if (((Posting)y).getVoteCount() < ((Posting)x).getVoteCount())
      {
        return -1;
      }
      return ((Posting)y).getVoteCount() > ((Posting)x).getVoteCount() ? 1 : 0;
    }
  }

  internal class PostingManager
  {
    [JsonIgnore]
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    [JsonIgnore]
    private readonly PostingPublishManager publishManager = new PostingPublishManager();
    [JsonIgnore]
    private bool postsCheckChangeFlag = true;

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
      if (Utilities.RUNNINGINTESTMODE)
      {
        Posting.DAYSUNTILDELETENORMAL = 2;
      }
      this.lastPostingId = 1;
      this.postings = new ConcurrentDictionary<long, Posting>();
      this.postingsToCheck = new ConcurrentQueue<Posting>();
      this.postingsInCheck = new ConcurrentDictionary<long, Posting>();
      this.postingsToPublish = new ConcurrentQueue<long>();
      this.postingsToPublishHappyHour = new ConcurrentQueue<long>();
      this.postingsToPublishPremiumHour = new ConcurrentQueue<long>();
      this.publishManager.calcNextSlot();
    }

    internal void addPosting(string text, long authorId)
    {
      Posting posting = new Posting(this.lastPostingId++,text, authorId);
      this.postingsToCheck.Enqueue(posting);
      this.postsCheckChangeFlag = true;
    }

    internal Posting tryPublish()
    {
      // Welche Stunde haben wir?
      Posting postToPublish = null;
      long postId = 0;
      try
      {
        switch (this.publishManager.getCurrentpublishType())
        {
          case PostingPublishManager.publishHourType.NORMAL:
            if (this.postingsToPublish.TryDequeue(out postId))
            {
              postToPublish = this.postings[postId];
            }
            break;
          case PostingPublishManager.publishHourType.HAPPY:
            if (this.postingsToPublishHappyHour.TryDequeue(out postId))
            {
              postToPublish = this.postings[postId];
            }
            break;
          case PostingPublishManager.publishHourType.PREMIUM:
            if (this.postingsToPublishPremiumHour.TryDequeue(out postId))
            {
              postToPublish = this.postings[postId];
            }
            break;
          default:
            break;
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Posting konnte nicht veröffentlicht werden, postId war: " + postId);
        postToPublish = null;
      }

      if(postToPublish != null)
      {
        postToPublish.setPublishTimestamp(new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0));
      }
      return postToPublish;
    }

    internal void testPublishing(long postingid, DateTime dateTime)
    {
      // Welche Stunde haben wir?
      Posting postToPublish = this.postings[postingid];        
      if (postToPublish != null)
      {
        postToPublish.setPublishTimestamp(dateTime);
      }
    }

    internal string acceptPost(long postingId, PostingPublishManager.publishHourType publishType)
    {
      if (!this.postingsInCheck.ContainsKey(postingId))
      {
        return "";
      }
      Posting postingToAccept = this.postingsInCheck[postingId];
      // Aus der einen Liste entfernen und in die Andere transferieren
      this.postingsInCheck.Remove(postingToAccept.getPostID(), out postingToAccept);
      this.postings.TryAdd(postingToAccept.getPostID(),postingToAccept);
      if(publishType == PostingPublishManager.publishHourType.PREMIUM)
      {
        DateTime nextFreeSlotPremium = this.publishManager.getTimestampOfNextSlot(this.postingsToPublishPremiumHour.Count, PostingPublishManager.publishHourType.PREMIUM);
        DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.publishHourType.NORMAL);
        if(nextFreeSlotPremium < nextFreeSlotNormal)
        {
          this.postingsToPublishPremiumHour.Enqueue(postingToAccept.getPostID());
          return "Veröffentlichung voraussichtlich: " + nextFreeSlotPremium.ToString(Utilities.usedCultureInfo);
        }
        else
        {
          this.postingsToPublish.Enqueue(postingToAccept.getPostID());
          return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString(Utilities.usedCultureInfo);
        }          
      }
      else
      {
        if(publishType == PostingPublishManager.publishHourType.HAPPY)
        {
          DateTime nextFreeSlotHappy = this.publishManager.getTimestampOfNextSlot(this.postingsToPublishHappyHour.Count, PostingPublishManager.publishHourType.HAPPY);
          DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.publishHourType.NORMAL);
          if (nextFreeSlotHappy < nextFreeSlotNormal)
          {
            this.postingsToPublishHappyHour.Enqueue(postingToAccept.getPostID());
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotHappy.ToString(Utilities.usedCultureInfo);
          }
          else
          {
            this.postingsToPublish.Enqueue(postingToAccept.getPostID());
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString(Utilities.usedCultureInfo);
          }
        }
        else
        {
          DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.publishHourType.NORMAL);
          this.postingsToPublish.Enqueue(postingToAccept.getPostID());
          return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString(Utilities.usedCultureInfo);
        }
      }
    }

    internal Posting getPostingInCheck(long nextPostModerationId)
    {     
      if (this.postingsInCheck.ContainsKey(nextPostModerationId))
      {
        return this.postingsInCheck[nextPostModerationId];
      }
      return null;
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
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].resetDirtyFlag();
      }
    }

    internal void resetTextDirtyFlag(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].resetTextDirtyFlag();
      }
    }

    internal void resetFlagged(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].resetFlagStatus();
      }
    }

    internal IEnumerable<long> getDirtyPosts()
    {
      List<long> postlist = new List<long>();
      foreach (Posting posting in this.postings.Values)
      {
        if (posting.isDirty())
        {
          postlist.Add(posting.getPostID());
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
            postlist.Add(posting.getPostID());
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
            postlist.Add(posting.getPostID());
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
      Posting post = null;
      if(this.postingsToCheck.TryDequeue(out post))
      {
        this.postsCheckChangeFlag = true;
        if (this.postingsInCheck.TryAdd(post.getPostID(), post))
        {
          return new KeyValuePair<long, string>(post.getPostID(), post.getPostingText());
        }
        else
        {
          logger.Error("Konnte den Post nicht zu den gerade geprüften hinzufügen, ID: " + post.getPostID());
        }
      }
      return new KeyValuePair<long, string>(-1, "");
    }

    internal bool putBackIntoQueue(long postingId)
    {
      if (this.postingsInCheck.TryRemove(postingId, out var posting))
      {
        // put back
        this.postingsToCheck.Enqueue(posting);
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
      if(this.postings.ContainsKey(postingId))
      {
        return this.postings[postingId].getPostingText().Substring(0, Math.Min(60, this.postings[postingId].getPostingText().Length)) + " [...]";
      }
      if(this.postingsInCheck.ContainsKey(postingId))
      {
        return this.postingsInCheck[postingId].getPostingText().Substring(0, Math.Min(60, this.postingsInCheck[postingId].getPostingText().Length)) + " [...]";
      }
      logger.Error("Konnte den Teaser nicht laden: " + postingId);
      return "... Post nicht gefunden ...";
    }

    internal long getAuthorId(long postingId)
    {
      if(this.postings.ContainsKey(postingId))
      {
        return this.postings[postingId].getAuthorID();
      }
      foreach (Posting p in this.postingsToCheck)
      {
        if (p.getPostID() == postingId)
        {
          return p.getAuthorID();
        }
      }
      if(this.postingsInCheck.ContainsKey(postingId))
      {
        return this.postingsInCheck[postingId].getAuthorID();
      }
      return -1;
    }

    internal void discardPost(long postingId)
    {
      if (this.postingsInCheck.ContainsKey(postingId))
      {
        Posting posting = null;
        this.postingsInCheck.TryRemove(postingId, out posting);
        return;
      }
      logger.Error("Konnte den Post mit der ID " + postingId + " nicht löschen.");
    }

    internal bool isAuthor(long postingId, long authorId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        return this.postings[postingId].getAuthorID() == authorId;
      }
      return false;
    }

    internal void upvote(long postingId, int votecount)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].voteup(votecount);
      }
    }

    internal void downvote(long postingId, int votecount)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].votedown(votecount);
      }
    }

    internal void flag(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].flag();
      }
    }

    internal int getMessageId(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getChatMessageID();
      }
      return -1;
    }

    internal int getMessageIdDaily(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getChatMessageDailyID();
      }
      return -1;
    }

    internal int getMessageIdWeekly(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getChatMessageWeeklyID();
      }
      return -1;
    }

    internal bool isTopPost(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getTopPostStatus();
      }
      return false;
    }

    internal long getUpVotes(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getUpVotes();
      }
      return 0;
    }

    internal long getDownVotes(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getDownVotes();
      }
      return 0;
    }

    internal string getPostingText(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        return this.postings[postingId].getPostingText();
      }
      return "";
    }

    internal string getPostingStatisticText(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        return this.postings[postingId].getPostStatisticText();
      }
      return "";
    }

    internal void setDailyChatMsgId(long postid, int messageId)
    {
      if (this.postings.ContainsKey(postid))
      {
        this.postings[postid].setChatMessageDailyID(messageId);
      }
    }

    internal void setWeeklyChatMsgId(long postid, int messageId)
    {
      if (this.postings.ContainsKey(postid))
      {
        this.postings[postid].setChatMessageWeeklyID(messageId);
      }
    }

    internal int getFlagCountOfPost(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings[postId].getFlagCount();
      }
      return -1;
    }

    internal bool removePost(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        return this.postings.TryRemove(postingId, out var posting);
      }
      return false;
    }

    internal bool removeFlagFromPost(long postingId)
    {
      if (this.postings.ContainsKey(postingId))
      {
        this.postings[postingId].resetFlagStatus();
        return true;
      }
      return false;
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
        if (statistics.isTopPost(posting.getUpVotes(), posting.getVoteCount()))
        {
          posting.setTopPostStatus(true);
        }
        else
        {
          posting.setTopPostStatus(false);
        }
      }
    }

    internal List<long> getPostsToDelete()
    {
      List<long> resultList = new List<long>();
      foreach (Posting posting in this.postings.Values)
      {
        if (posting.shouldBeDeleted())
        {
          resultList.Add(posting.getPostID());
        }
      }
      return resultList;
    }

    internal List<long> getDailyTopPostsFromYesterday()
    {
      // Iteriere über alle Posts, filtern nach Gestern, Sortieren nach Votes, Top 3 zurück
      DateTime yesterday = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
      yesterday = yesterday.AddDays(-1.0);
      List<Posting> result = new List<Posting>();
      foreach(Posting posting in this.postings.Values)
      {
        TimeSpan diff = posting.getPublishTimestamp() - yesterday;
        if (diff.TotalHours >= 0.0 && diff.TotalHours <= 24.0 )
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
          ((Posting)targetlist.GetValue(0)).getPostID(),
          ((Posting)targetlist.GetValue(1)).getPostID(),
          ((Posting)targetlist.GetValue(2)).getPostID()
        };
        return resultList;
      }
      else
      {
        List<long> resultList = new List<long>();
        foreach (Posting posting in result)
        {
          resultList.Add(posting.getPostID());
        }
        return resultList;
      }
    }


    internal List<long> getWeeklyTopPostsFromLastWeek()
    {
      // Iteriere über alle Posts, filtern nach Gestern, Sortieren nach Votes, Top 3 zurück
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
          ((Posting)targetlist.GetValue(0)).getPostID(),
          ((Posting)targetlist.GetValue(1)).getPostID(),
          ((Posting)targetlist.GetValue(2)).getPostID(),
          ((Posting)targetlist.GetValue(3)).getPostID(),
          ((Posting)targetlist.GetValue(4)).getPostID()
        };
        return resultList;
      }
      else
      {
        List<long> resultList = new List<long>();
        foreach (Posting posting in result)
        {
          resultList.Add(posting.getPostID());
        }
        return resultList;
      }
    }

    internal long getMedianVotes()
    {
      List<long> voteCounts = new List<long>();
      foreach (Posting posting in this.postings.Values)
      {
        voteCounts.Add(posting.getVoteCount());
      }
      if (voteCounts.Count > 1)
      {
        long[] countArray = voteCounts.ToArray();
        Array.Sort(countArray);
        return countArray[countArray.Length / 2];
      }
      return 5;
    }


    public bool deletePost(long postId)
    {
      if (this.postings.ContainsKey(postId))
      {
        return this.postings.Remove(postId,out _);
      }
      return false;
    }
  }


}
