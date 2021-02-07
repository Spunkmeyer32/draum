using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("DRaumServerTest")]

namespace DRaumServerApp
{
  
  class Posting
  {
    [JsonIgnore]
    private readonly object textMutex = new object();
    [JsonIgnore]
    private readonly object flagDataMutex = new object();
    [JsonIgnore]
    private static readonly int DAYSUNTILDELETENORMAL = 76;



    [JsonIgnore]
    private readonly object upvoteMutex = new object();
    [JsonIgnore]
    private readonly object downvoteMutex = new object();
    [JsonIgnore]
    private readonly object publishTimestampMutex = new object();

    [JsonProperty]
    private readonly long postingId;
    [JsonProperty]
    private readonly long authorID;
    [JsonProperty]
    private readonly DateTime submitTimestamp;

    [JsonProperty]
    private volatile int chatMessageId;
    [JsonProperty]
    private volatile bool flagged;
    [JsonProperty]
    private volatile bool postedInDaily;
    [JsonProperty]
    private volatile bool postedInWeekly;
    [JsonProperty]
    private volatile int daysBeforeDelete;
    [JsonProperty]
    private volatile bool isTopPost;
    [JsonProperty]
    private volatile int flagCount;
    [JsonProperty]
    private volatile bool dirtyFlag; // Markiert den Post für ein Update im Chat
    [JsonProperty]
    private ConcurrentBag<long> votedUsers;
    [JsonProperty]
    private ConcurrentBag<long> flaggedUsers;
    [JsonProperty]
    private String postText;   
    [JsonProperty]
    private long upVotes;
    [JsonProperty]
    private long downVotes;
    [JsonProperty]
    private DateTime publishTimestamp;
    

    internal Posting() 
    {
      this.postingId = -1;
      this.authorID = -1;
      setDefaults();
      this.submitTimestamp = DateTime.Now;
    }

    internal Posting(long id, string text, long authorID)
    {      
      this.postingId = id;
      this.authorID = authorID;
      setDefaults();      
      this.postText = text;
      this.submitTimestamp = DateTime.Now;
    }

    private void setDefaults()
    {      
      this.chatMessageId = -1;
      this.postText = "";      
      this.flagged = false;
      this.votedUsers = new ConcurrentBag<long>();
      this.flaggedUsers = new ConcurrentBag<long>();
      this.flagCount = 0;
      this.upVotes = 0;
      this.downVotes = 0;      
      this.publishTimestamp = new DateTime(1999, 1, 1);
      this.dirtyFlag = false;
      this.isTopPost = false;
      this.postedInDaily = false;
      this.postedInWeekly = false;
      this.daysBeforeDelete = DAYSUNTILDELETENORMAL;
    }

    internal DateTime getPublishTimestamp()
    {
      lock (this.publishTimestampMutex)
      {
        return this.publishTimestamp;
      }
    }

    internal int getVoteCount()
    {
      return this.votedUsers.Count;
    }

    internal long getPostID()
    {
      return this.postingId;
    }

    internal long getAuthorID()
    {
      return this.authorID;
    }

    internal string getPostingText()
    {
      lock (this.textMutex)
      {
        return this.postText;
      }      
    }
    internal void setPublishTimestamp(DateTime dateTime)
    {
      lock (this.publishTimestampMutex)
      {
        this.publishTimestamp = dateTime;
      }
    }

    internal void updateText(string text)
    {
      lock (this.textMutex)
      {
        this.postText = text;
      }      
    }

    internal void calculateDaysToDelete()
    {
      if(this.isTopPost)
      {
        this.daysBeforeDelete = DAYSUNTILDELETENORMAL + (int)((float)(this.getUpVotePercentage() - 50) * 1.5);
      }
      else
      {
        this.daysBeforeDelete = DAYSUNTILDELETENORMAL + (int)((float)(this.getUpVotePercentage() - 50) * 1.2);
      }
    }

    internal string getPostStatisticText()
    {
      lock (this.publishTimestampMutex)
      {
        return "<i>Veröffentlicht am " + this.publishTimestamp.ToShortDateString() + " um " + this.publishTimestamp.ToShortTimeString() + " Uhr</i>";
      }
    }

    internal bool canUserVote(long id)
    {
      if(!Utilities.RUNNINGINTESTMODE)
      { 
        if (this.authorID == id)
        {
          return false;
        }
      }
      if (this.votedUsers.Contains(id))
      {
        return false;
      }
      return true;
    }

    internal bool canUserFlag(long id)
    {
      if (!Utilities.RUNNINGINTESTMODE)
      {
        if (this.authorID == id)
        {
          return false;
        }
      }
      if (this.flaggedUsers.Contains(id))
      {
        return false;
      }         
      return true;
    }

    internal void voteup(long userID, int votecount)
    {
      lock (this.upvoteMutex)
      {
        this.upVotes = this.upVotes + votecount;
        this.dirtyFlag = true;
      }      
      this.votedUsers.Add(userID);
    }

    internal void votedown(long userID, int votecount)
    {
      lock (this.downvoteMutex)
      {
        this.downVotes = this.downVotes + votecount;
        this.dirtyFlag = true;
      }
      this.votedUsers.Add(userID);
    }

    internal bool isDirty()
    {
      return this.dirtyFlag;
    }

    internal void resetDirtyFlag()
    {
      this.dirtyFlag = false;
    }

    internal void flag(long userID)
    {
      lock (this.flagDataMutex)
      {
        this.flagCount = this.flagCount + 1;
        this.flagged = true;
      }      
      this.flaggedUsers.Add(userID);
    }

    internal int getFlagCount()
    {
      return this.flagCount;
    }

    internal bool isFlagged()
    {
      lock(this.flagDataMutex)
      {
        return this.flagged;
      }
    }

    internal void resetFlagStatus()
    {
      lock(this.flagDataMutex)
      {
        this.flagged = false;
      }
    }

    internal int getUpVotePercentage()
    {
      if(this.upVotes.Equals(0) && this.downVotes.Equals(0))
      {
        return 50;
      }
      float r = (float)this.upVotes / (float)(this.upVotes + this.downVotes);
      return (int)(Math.Ceiling((r * 100.0f)));
    }

    internal void setChatMessageID(int messageId)
    {
      this.chatMessageId = messageId;
    }

    internal int getChatMessageID()
    {
      return this.chatMessageId;
    }
  }
}
