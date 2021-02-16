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
      return ((Posting)y).getVoteCount() - ((Posting)x).getVoteCount();
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
      this.lastPostingId = 1;
      this.postings = new ConcurrentDictionary<long, Posting>();
      this.postingsToCheck = new ConcurrentQueue<Posting>();
      this.postingsInCheck = new ConcurrentDictionary<long, Posting>();
      this.postingsToPublish = new ConcurrentQueue<long>();
      this.postingsToPublishHappyHour = new ConcurrentQueue<long>();
      this.postingsToPublishPremiumHour = new ConcurrentQueue<long>();
      this.publishManager.calcNextSlot();
    }

    internal void addPosting(string text, long authorID)
    {
      Posting posting = new Posting(this.lastPostingId++,text, authorID);
      this.postingsToCheck.Enqueue(posting);
      this.postsCheckChangeFlag = true;
    }

    internal Posting tryPublish()
    {
      // Welche Stunde haben wir?
      Posting postToPublish = null;
      long postId = 0;
      switch (this.publishManager.getCurrentpublishType())
      {
        case PostingPublishManager.publishHourType.NORMAL:
          if (this.postingsToPublish.Count > 0)
          {
            if (this.postingsToPublish.TryDequeue(out postId))
            {
              postToPublish = this.postings[postId];
            }
          }
          break;
        case PostingPublishManager.publishHourType.HAPPY:
          if (this.postingsToPublishHappyHour.Count > 0)
          {
            if (this.postingsToPublishHappyHour.TryDequeue(out postId))
            {
              postToPublish = this.postings[postId];
            }
          }
          break;
        case PostingPublishManager.publishHourType.PREMIUM:
          if (this.postingsToPublishPremiumHour.Count > 0)
          {
            if (this.postingsToPublishPremiumHour.TryDequeue(out postId))
            {
              postToPublish = this.postings[postId];
            }
          }
          break;
        default:
          break;
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

    internal void resetDirtyFlag(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].resetDirtyFlag();
      }
    }

    internal void resetTextDirtyFlag(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].resetTextDirtyFlag();
      }
    }

    internal void resetFlagged(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].resetFlagStatus();
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

    internal Posting removePostFromInCheck(long postID)
    {
      if(!this.postingsInCheck.TryRemove(postID, out var posting))
      {
        logger.Error("Konnte den Post nicht aus der Liste InCheck löschen: " + postID);
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

    internal long getAuthorID(long postingId)
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

    internal void discardPost(long postingID)
    {
      if (this.postingsInCheck.ContainsKey(postingID))
      {
        Posting posting = null;
        this.postingsInCheck.TryRemove(postingID, out posting);
        return;
      }
      logger.Error("Konnte den Post mit der ID " + postingID + " nicht löschen.");
    }

    internal bool canUserVote(long postingid, long id)
    {
      if (this.postings.ContainsKey(postingid))
      {
        return this.postings[postingid].canUserVote(id);
      }
      return false;
    }

    internal bool canUserFlag(long postingid, long id)
    {
      if (this.postings.ContainsKey(postingid))
      {
        return this.postings[postingid].canUserFlag(id);
      }
      return false;
    }

    internal void upvote(long postingID, long id, int votecount)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].voteup(id, votecount);
      }
    }

    internal void downvote(long postingID, long id, int votecount)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].votedown(id, votecount);
      }
    }

    internal void flag(long postingID, long userID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].flag(userID);
      }
    }

    internal int getMessageID(long postID)
    {
      if (this.postings.ContainsKey(postID))
      {
        return this.postings[postID].getChatMessageID();
      }
      return -1;
    }

    internal int getMessageIdDaily(long postID)
    {
      if (this.postings.ContainsKey(postID))
      {
        return this.postings[postID].getChatMessageDailyID();
      }
      return -1;
    }

    internal int getMessageIdWeekly(long postID)
    {
      if (this.postings.ContainsKey(postID))
      {
        return this.postings[postID].getChatMessageWeeklyID();
      }
      return -1;
    }

    internal bool isTopPost(long postID)
    {
      if (this.postings.ContainsKey(postID))
      {
        return this.postings[postID].getTopPostStatus();
      }
      return false;
    }

    internal int getUpVotePercentage(long postID)
    {
      if (this.postings.ContainsKey(postID))
      {
        return this.postings[postID].getUpVotePercentage();
      }
      return 50;
    }

    internal string getPostingText(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        return this.postings[postingID].getPostingText();
      }
      return "";
    }

    internal string getPostingStatisticText(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        return this.postings[postingID].getPostStatisticText();
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

    internal int getFlagCountOfPost(long postID)
    {
      if (this.postings.ContainsKey(postID))
      {
        return this.postings[postID].getFlagCount();
      }
      return -1;
    }

    internal bool removePost(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        return this.postings.TryRemove(postingID, out var posting);
      }
      return false;
    }

    internal bool removeFlagFromPost(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        this.postings[postingID].resetFlagStatus();
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
        if (statistics.isTopPost(posting.getUpVotePercentage(), posting.getVoteCount()))
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
