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
      return ((Posting)y).getVoteCount() - ((Posting)x).getVoteCount();
    }
  }

  class PostingManager
  {
    [JsonIgnore]
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    [JsonIgnore]
    private PostingPublishManager publishManager = new PostingPublishManager();

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
    private Queue<long> postingsToPublish;


    [JsonProperty]
    private Queue<long> postingsToPublishHappyHour;
    [JsonProperty]
    private Queue<long> postingsToPublishPremiumHour;

    internal PostingManager()
    {
      this.lastPostingId = 1;
      this.postings = new ConcurrentDictionary<long, Posting>();
      this.postingsToCheck = new ConcurrentQueue<Posting>();
      this.postingsInCheck = new ConcurrentDictionary<long, Posting>();
      this.postingsToPublish = new Queue<long>();
      this.postingsToPublishHappyHour = new Queue<long>();
      this.postingsToPublishPremiumHour = new Queue<long>();
      this.publishManager.calcNextSlot();
    }

    internal void addPosting(String text, long authorID)
    {
      Posting posting = new Posting(this.lastPostingId++,text, authorID);
      this.postingsToCheck.Enqueue(posting);
      this.postsCheckChangeFlag = true;
    }

    internal Posting tryPublish()
    {
      // Welche Stunde haben wir?
      Posting postToPublish = null;
      switch (this.publishManager.getCurrentpublishType())
      {
        case PostingPublishManager.publishHourType.NORMAL:
          if (this.postingsToPublish.Count > 0)
          {
            postToPublish = this.postings[this.postingsToPublish.Dequeue()];
          }
          break;
        case PostingPublishManager.publishHourType.HAPPY:
          if (this.postingsToPublishHappyHour.Count > 0)
          {
            postToPublish = this.postings[this.postingsToPublishHappyHour.Dequeue()];
          }
          break;
        case PostingPublishManager.publishHourType.PREMIUM:
          if (this.postingsToPublishPremiumHour.Count > 0)
          {
            postToPublish = this.postings[this.postingsToPublishPremiumHour.Dequeue()];
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

    internal String acceptPost(long postingID, PostingPublishManager.publishHourType publishType)
    {
      if (this.postingsInCheck.ContainsKey(postingID))
      {
        Posting postingToAccept = this.postingsInCheck[postingID];
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
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotPremium.ToString();
          }
          else
          {
            this.postingsToPublish.Enqueue(postingToAccept.getPostID());
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString();
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
              return "Veröffentlichung voraussichtlich: " + nextFreeSlotHappy.ToString();
            }
            else
            {
              this.postingsToPublish.Enqueue(postingToAccept.getPostID());
              return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString();
            }
          }
          else
          {
            DateTime nextFreeSlotNormal = this.publishManager.getTimestampOfNextSlot(this.postingsToPublish.Count, PostingPublishManager.publishHourType.NORMAL);
            this.postingsToPublish.Enqueue(postingToAccept.getPostID());
            return "Veröffentlichung voraussichtlich: " + nextFreeSlotNormal.ToString();
          }
        }
      }
      return "";
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

    internal IEnumerable<long> getFlaggedPosts()
    {
      List<long> postlist = new List<long>();
      foreach (Posting posting in this.postings.Values)
      {
        if (posting.isFlagged())
        {
          postlist.Add(posting.getPostID());
        }
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

    internal bool putBackIntoQueue(long postID)
    {
      Posting posting;
      if (this.postingsInCheck.TryRemove(postID, out posting))
      {
        // put back
        this.postingsToCheck.Enqueue(posting);
        return true;
      }
      else
      {
        logger.Error("Posting nicht in der Liste In-Check gefunden: " + postID);
      }
      return false;
    }

    internal Posting removePostFromInCheck(long postID)
    {
      Posting posting;     
      if(!this.postingsInCheck.TryRemove(postID, out posting))
      {
        logger.Error("Konnte den Post nicht aus der Liste InCheck löschen: " + postID);
        return null;
      }
      return posting;
    }

    internal String getPostingTeaser(long postingID)
    {
      if(this.postings.ContainsKey(postingID))
      {
        return this.postings[postingID].getPostingText().Substring(0, Math.Min(60, this.postings[postingID].getPostingText().Length)) + " [...]";
      }
      if(this.postingsInCheck.ContainsKey(postingID))
      {
        return this.postingsInCheck[postingID].getPostingText().Substring(0, Math.Min(60, this.postingsInCheck[postingID].getPostingText().Length)) + " [...]";
      }
      logger.Error("Konnte den Teaser nicht laden: " + postingID);
      return "... Post nicht gefunden ...";
    }

    internal long getAuthorID(long postingID)
    {
      if(this.postings.ContainsKey(postingID))
      {
        return this.postings[postingID].getAuthorID();
      }
      foreach (Posting p in this.postingsToCheck)
      {
        if (p.getPostID() == postingID)
        {
          return p.getAuthorID();
        }
      }
      if(this.postingsInCheck.ContainsKey(postingID))
      {
        return this.postingsInCheck[postingID].getAuthorID();
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

    internal bool removePost(long postingID)
    {
      if (this.postings.ContainsKey(postingID))
      {
        Posting posting;
        return this.postings.TryRemove(postingID, out posting);
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

    internal List<Posting> getDailyTopPostsFromYesterday()
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
        List<Posting> resultList = new List<Posting>
        {
          (Posting)targetlist.GetValue(0),
          (Posting)targetlist.GetValue(1),
          (Posting)targetlist.GetValue(2)
        };
        return resultList;
      }
      else
      {
        return new List<Posting>( result.ToArray() );
      }
      
    }
  }


}
