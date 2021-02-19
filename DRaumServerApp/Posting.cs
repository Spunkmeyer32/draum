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
    internal static int DAYSUNTILDELETENORMAL = 76;
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
    private volatile int chatMessageDailyId;
    [JsonProperty]
    private volatile int chatMessageWeeklyId;
    [JsonProperty]
    private volatile bool flagged;
    [JsonProperty]
    private volatile int daysBeforeDelete;
    [JsonProperty]
    private volatile bool isTopPost;
    [JsonProperty]
    private volatile int flagCount;
    [JsonProperty]
    private volatile bool dirtyFlag; // Markiert den Post für ein Update im Chat
    [JsonProperty]
    private volatile bool dirtyTextFlag; // Markiert den Post für ein Update im Chat
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
      this.setDefaults();
      this.submitTimestamp = DateTime.Now;
    }

    internal Posting(long id, string text, long authorID)
    {      
      this.postingId = id;
      this.authorID = authorID;
      this.setDefaults();      
      this.postText = text;
      this.submitTimestamp = DateTime.Now;
    }

    private void setDefaults()
    {      
      this.chatMessageId = -1;
      this.chatMessageDailyId = -1;
      this.chatMessageWeeklyId = -1;
      this.postText = "";      
      this.flagged = false;
      this.flagCount = 0;
      this.upVotes = 0;
      this.downVotes = 0;      
      this.publishTimestamp = new DateTime(1999, 1, 1);
      this.dirtyFlag = false;
      this.dirtyTextFlag = false;
      this.isTopPost = false;
      this.daysBeforeDelete = DAYSUNTILDELETENORMAL;
    }

    internal DateTime getPublishTimestamp()
    {
      lock (this.publishTimestampMutex)
      {
        return this.publishTimestamp;
      }
    }

    internal long getVoteCount()
    {
      return this.upVotes + this.downVotes;
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
        if (!this.publishTimestamp.Equals(dateTime))
        {
          this.publishTimestamp = dateTime;
          this.dirtyTextFlag = true;
        }
      }
    }

    internal void updateText(string text)
    {
      lock (this.textMutex)
      {
        if (!text.Equals(this.postText))
        {
          this.postText = text;
          this.dirtyTextFlag = true;
        }
      }      
    }

    internal void updateText(string text, bool dontSetDirtyFlag)
    {
      lock (this.textMutex)
      {
        if (!text.Equals(this.postText))
        {
          this.postText = text;
          if (!dontSetDirtyFlag)
          {
            this.dirtyTextFlag = true;
          }
        }
      }      
    }

    private void calculateDaysToDelete()
    {
      int newDays = 0;
      if(this.isTopPost)
      {
        newDays = DAYSUNTILDELETENORMAL + (int)((float)(this.getUpVotePercentage() - 50) * 1.5);
      }
      else
      {
        newDays = DAYSUNTILDELETENORMAL + (int)((float)(this.getUpVotePercentage() - 50) * 1.2);
      }
      if (newDays != this.daysBeforeDelete)
      {
        this.daysBeforeDelete = newDays;
        this.dirtyTextFlag = true;
      }
    }

    internal int getUpVotePercentage()
    {
      if (this.upVotes.Equals(0) && this.downVotes.Equals(0))
      {
        return 50;
      }
      float r = (float)this.upVotes / (float)(this.upVotes + this.downVotes);
      return (int)(Math.Ceiling((r * 100.0f)));
    }

    internal string getPostStatisticText()
    {
      lock (this.publishTimestampMutex)
      {
        string result = "<i>Veröffentlicht am " + this.publishTimestamp.ToShortDateString() + " um " + this.publishTimestamp.ToShortTimeString() + " Uhr</i>";
        DateTime deleteTime = this.publishTimestamp.AddDays(this.daysBeforeDelete);
        result += "\r\n" + "<i>Wird voraussichtlich am " + deleteTime.ToShortDateString() + " gelöscht</i>";
        return result;
      }
    }

    internal bool shouldBeDeleted()
    {
      lock (this.publishTimestampMutex)
      {
        if (this.publishTimestamp.Year <= 2000 || this.chatMessageId == -1)
        {
          return false;
        }
        if (this.publishTimestamp.AddDays(this.daysBeforeDelete) < DateTime.Now)
        {
          return true;
        }
      }
      return false;
    }

    

    internal void setTopPostStatus(bool status)
    {
      if (this.isTopPost != status)
      {
        this.isTopPost = status;
        this.calculateDaysToDelete();
        this.dirtyTextFlag = true;
      }
    }

    internal void voteup(int votecount)
    {
      lock (this.upvoteMutex)
      {
        this.upVotes = this.upVotes + votecount;
        this.calculateDaysToDelete();
        this.dirtyFlag = true;
      }     
    }

    internal void votedown(int votecount)
    {
      lock (this.downvoteMutex)
      {
        this.downVotes = this.downVotes + votecount;
        this.calculateDaysToDelete();
        this.dirtyFlag = true;
      }
    }

    internal bool isDirty()
    {
      return this.dirtyFlag;
    }

    internal void resetDirtyFlag()
    {
      this.dirtyFlag = false;
    }

    internal bool isTextDirty()
    {
      return this.dirtyTextFlag;
    }

    internal void resetTextDirtyFlag()
    {
      this.dirtyTextFlag = false;
    }

    internal void flag()
    {
      lock (this.flagDataMutex)
      {
        this.flagCount = this.flagCount + 1;
        this.flagged = true;
      }      
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

    public long getUpVotes()
    {
      return this.upVotes;
    }

    public long getDownVotes()
    {
      return this.downVotes;
    }

    internal void setChatMessageID(int messageId)
    {
      this.chatMessageId = messageId;
    }

    internal int getChatMessageID()
    {
      return this.chatMessageId;
    }

    internal void setChatMessageDailyID(int messageId)
    {
      this.chatMessageDailyId = messageId;
    }

    internal int getChatMessageDailyID()
    {
      return this.chatMessageDailyId;
    }

    internal void setChatMessageWeeklyID(int messageId)
    {
      this.chatMessageWeeklyId = messageId;
    }

    internal int getChatMessageWeeklyID()
    {
      return this.chatMessageWeeklyId;
    }

    internal bool getTopPostStatus()
    {
      return this.isTopPost;
    }

    
  }
}
