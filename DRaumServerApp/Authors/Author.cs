using System;
using System.Collections.Concurrent;
using System.Linq;
using DRaumServerApp.Postings;
using Newtonsoft.Json;

namespace DRaumServerApp.Authors
{
  internal class Author
  {

    public enum InteractionCooldownTimer {  Default, Posting, Flagging, Feedback };

    internal static int Cooldownminutes = 2;
    internal static int Cooldownminutesflagging = 30;
    internal static int Cooldownhoursposting = 3;
    internal static int Cooldownhoursfeedback = 1;
    private static int _expForPosting = 16;
    private static int _expForVote = 7;

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
      return !this.votedPosts.Contains(postId);
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
      return !this.flaggedPosts.Contains(postId);
    }

    internal PostingPublishManager.PublishHourType getPublishType(int premiumLevelCap)
    {
      if (Utilities.Runningintestmode)
      {
        return PostingPublishManager.PublishHourType.Normal;
      }
      if (this.level > premiumLevelCap)
      {
        return PostingPublishManager.PublishHourType.Premium;
      }
      if (this.postCount < 3)
      {
        return PostingPublishManager.PublishHourType.Happy;
      }
      return PostingPublishManager.PublishHourType.Normal;
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

    internal void publishedSuccessfully()
    {
      this.exp += _expForPosting;
      this.postCount += 1;
    }

    private long getTotalVotes()
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
      DateTime cooldownTs = this.coolDownTimeStamp;
      switch(timerType)
      {
        case InteractionCooldownTimer.Feedback:
          cooldownTs = this.coolDownTimeStampFeedback;
          break;
        case InteractionCooldownTimer.Flagging:
          cooldownTs = this.coolDownTimeStampFlagging;
          break;
        case InteractionCooldownTimer.Posting:
          cooldownTs = this.coolDownTimeStampPosting;
          break;
      }
      if(cooldownTs < DateTime.Now)
      {
        return true;
      }
      return false;
    }

    internal TimeSpan getCoolDownTimer(InteractionCooldownTimer timerType)
    {
      DateTime cooldownTs = this.coolDownTimeStamp;
      switch (timerType)
      {
        case InteractionCooldownTimer.Feedback:
          cooldownTs = this.coolDownTimeStampFeedback;
          break;
        case InteractionCooldownTimer.Flagging:
          cooldownTs = this.coolDownTimeStampFlagging;
          break;
        case InteractionCooldownTimer.Posting:
          cooldownTs = this.coolDownTimeStampPosting;
          break;
      }
      return cooldownTs.Subtract(DateTime.Now);
    }

    internal void resetCoolDown(InteractionCooldownTimer timerType)
    {
      switch (timerType)
      {
        case InteractionCooldownTimer.Feedback:
          this.coolDownTimeStampFeedback = DateTime.Now.AddHours(Cooldownhoursfeedback);
          break;
        case InteractionCooldownTimer.Flagging:
          this.coolDownTimeStampFlagging = DateTime.Now.AddMinutes(Cooldownminutesflagging);
          break;
        case InteractionCooldownTimer.Posting:
          this.coolDownTimeStampPosting = DateTime.Now.AddHours(Cooldownhoursposting);
          break;
        case InteractionCooldownTimer.Default:
          this.coolDownTimeStamp = DateTime.Now.AddMinutes(Cooldownminutes);
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
      this.exp += _expForVote;
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
      this.exp += _expForVote;
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
