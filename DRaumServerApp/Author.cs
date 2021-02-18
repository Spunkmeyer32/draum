using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace DRaumServerApp
{
  class Author
  {

    public enum InteractionCooldownTimer { NONE, DEFAULT, POSTING, FLAGGING, FEEDBACK };

    internal static int COOLDOWNMINUTES = 2;
    internal static int COOLDOWNMINUTESFLAGGING = 30;
    internal static int COOLDOWNHOURSPOSTING = 3;
    internal static int COOLDOWNHOURSFEEDBACK = 1;
    private static int EXP_FOR_POSTING = 16;
    private static int EXP_FOR_VOTE = 7;

    [JsonIgnore]
    private DateTime coolDownTimeStamp;
    [JsonIgnore]
    private DateTime coolDownTimeStampFlagging;
    [JsonIgnore]
    private DateTime coolDownTimeStampPosting;
    [JsonIgnore]
    private DateTime coolDownTimeStampFeedback;

    [JsonProperty]
    private long authorId;
    [JsonProperty]
    private string authorName;
    [JsonProperty]
    private bool postmode;
    [JsonProperty]
    private bool feedbackmode;    
    [JsonProperty]
    private int votingGauge;
    [JsonProperty]
    private long postCount;
    [JsonProperty]
    private long exp;
    [JsonProperty]
    private int level;
    [JsonProperty]
    private long upvotesReceived;
    [JsonProperty]
    private long downvotesReceived;
    [JsonProperty]
    private ConcurrentBag<long> votedPosts;
    [JsonProperty]
    private ConcurrentBag<long> flaggedPosts;

    internal Author()
    {
      this.loadDefaults();
    }

    internal Author(long authorId, string authorName)
    {
      this.loadDefaults();
      this.authorId = authorId;
      this.authorName = authorName;     
    }

    private void loadDefaults()
    {
      this.authorId = -1;
      this.authorName = "";
      this.postmode = false;
      this.feedbackmode = false;
      this.coolDownTimeStamp = DateTime.Now;
      this.votedPosts = new ConcurrentBag<long>();
      this.flaggedPosts = new ConcurrentBag<long>();
      this.votingGauge = 0;
      this.level = 1;
      this.exp = 0;
      this.postCount = 0;
      this.upvotesReceived = 0;
      this.downvotesReceived = 0;
    }

    internal bool canVote(long postId)
    {
      if (this.votedPosts.Contains(postId))
      {
        return false;
      }
      return true;
    }

    internal void vote(long postingId)
    {
      this.votedPosts.Add(postingId);
    }

    internal void flag(long postingId)
    {
      this.flaggedPosts.Add(postingId);
    }

    internal bool canFlag(long postId)
    {
      if (this.flaggedPosts.Contains(postId))
      {
        return false;
      }         
      return true;
    }

    internal PostingPublishManager.publishHourType getPublishType(int premiumLevelCap)
    {
      if (!Utilities.RUNNINGINTESTMODE)
      {
        if (this.level > premiumLevelCap)
        {
          return PostingPublishManager.publishHourType.PREMIUM;
        }
        if (this.postCount < 3)
        {
          return PostingPublishManager.publishHourType.HAPPY;
        }
      }
      return PostingPublishManager.publishHourType.NORMAL;
    }

    internal void updateCredibility(long receivedUpVotes, long receivedDownVotes)
    {
      this.downvotesReceived += receivedDownVotes;
      this.upvotesReceived += receivedUpVotes;   
    }

    public int getLevel()
    {
      if(this.level>=500)
      {
        return 500;
      }
      while(this.exp >= Utilities.getNextLevelExp(this.level))
      {
        this.level++;       
      }
      return this.level;
    }

    internal string getShortAuthorInfo()
    {
      int percentage = 50;
      if(this.getTotalVotes() != 0)
      {
        percentage = (int)((this.upvotesReceived / (float)this.getTotalVotes()) * 100.0f);
      }
      return "Level " + this.getLevel() + " Schreiber/in mit " + percentage + " Prozent  Zustimmung";
    }

    internal string getFullAuthorInfo()
    {
      return "@"+this.authorName + " ("+this.authorId+")\r\n" + this.getShortAuthorInfo();
    }

    internal void publishedSuccessfully()
    {
      this.exp += EXP_FOR_POSTING;
      this.postCount += 1;
    }

    internal long getTotalVotes()
    {
      return this.upvotesReceived + this.downvotesReceived;
    }

    internal string getAuthorName()
    {
      return this.authorName;
    }

    internal long getAuthorId()
    {
      return this.authorId;
    }

    internal void setPostMode()
    {
      this.postmode = true;
    }

    internal bool isInPostMode()
    {
      return this.postmode;
    }

    internal void unsetModes()
    {
      this.postmode = false;
      this.feedbackmode = false;
    }

    internal bool coolDownOver(InteractionCooldownTimer timerType)
    {
      DateTime cooldownTS = this.coolDownTimeStamp;
      switch(timerType)
      {
        case InteractionCooldownTimer.FEEDBACK:
          cooldownTS = this.coolDownTimeStampFeedback;
          break;
        case InteractionCooldownTimer.FLAGGING:
          cooldownTS = this.coolDownTimeStampFlagging;
          break;
        case InteractionCooldownTimer.POSTING:
          cooldownTS = this.coolDownTimeStampPosting;
          break;
      }
      if(cooldownTS < DateTime.Now)
      {
        return true;
      }
      return false;
    }

    internal TimeSpan getCoolDownTimer(InteractionCooldownTimer timerType)
    {
      DateTime cooldownTS = this.coolDownTimeStamp;
      switch (timerType)
      {
        case InteractionCooldownTimer.FEEDBACK:
          cooldownTS = this.coolDownTimeStampFeedback;
          break;
        case InteractionCooldownTimer.FLAGGING:
          cooldownTS = this.coolDownTimeStampFlagging;
          break;
        case InteractionCooldownTimer.POSTING:
          cooldownTS = this.coolDownTimeStampPosting;
          break;
      }
      return cooldownTS.Subtract(DateTime.Now);
    }

    internal void resetCoolDown(InteractionCooldownTimer timerType)
    {
      switch (timerType)
      {
        case InteractionCooldownTimer.FEEDBACK:
          this.coolDownTimeStampFeedback = DateTime.Now.AddHours(COOLDOWNHOURSFEEDBACK);
          break;
        case InteractionCooldownTimer.FLAGGING:
          this.coolDownTimeStampFlagging = DateTime.Now.AddMinutes(COOLDOWNMINUTESFLAGGING);
          break;
        case InteractionCooldownTimer.POSTING:
          this.coolDownTimeStampPosting = DateTime.Now.AddHours(COOLDOWNHOURSPOSTING);
          break;
        case InteractionCooldownTimer.DEFAULT:
          this.coolDownTimeStamp = DateTime.Now.AddMinutes(COOLDOWNMINUTES);
          break;
      }
    }

    internal void setFeedbackMode()
    {
      this.feedbackmode = true;
    }

    internal bool isInFeedbackMode()
    {
      return this.feedbackmode;
    }

    internal int voteUpAndGetCount()
    {
      this.exp += EXP_FOR_VOTE;
      if (this.votingGauge <= 0)
      {
        this.votingGauge += 1;
        return 10;
      }
      else
      {
        int result = Math.Max(10 - (this.votingGauge / 2), 5);
        this.votingGauge += 1;
        if (this.votingGauge > 10)
        {
          this.votingGauge = 10;
        }
        return result;
      }
    }

    internal int voteDownAndGetCount()
    {
      this.exp += EXP_FOR_VOTE;
      if (this.votingGauge >= 0)
      {
        this.votingGauge -= 1;
        return 10;
      }
      else
      {
        int result = Math.Max(10 + (this.votingGauge), 1);
        this.votingGauge -= 1;
        if (this.votingGauge < -10)
        {
          this.votingGauge = -10;
        }
        return result;
      }
    }

  }
    
}
