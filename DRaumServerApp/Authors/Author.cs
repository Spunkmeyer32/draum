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

    private static int _cooldownminutes = 2;
    private static int _cooldownminutesflagging = 30;
    private static int _cooldownhoursposting = 3;
    private static int _cooldownhoursfeedback = 1;
    private const int ExpForPosting = 16;
    private const int ExpForVote = 7;

    [JsonIgnore]
    private DateTime coolDownTimeStamp;
    [JsonIgnore]
    private DateTime coolDownTimeStampFlagging;
    [JsonIgnore]
    private DateTime coolDownTimeStampPosting;
    [JsonIgnore]
    private DateTime coolDownTimeStampFeedback;

    [JsonIgnore] 
    private readonly object credibilityMutex = new object();
    [JsonIgnore] 
    private readonly object votingGaugeMutex = new object();

    [JsonProperty]
    private long authorId;
    [JsonProperty]
    private string authorName;
    [JsonProperty]
    private DateTime lastActivity;
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
    [JsonProperty]
    private DateTime blockedUntil;

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
      this.lastActivity = DateTime.Now;
      this.votedPosts = new ConcurrentBag<long>();
      this.flaggedPosts = new ConcurrentBag<long>();
      this.votingGauge = 0;
      this.level = 1;
      this.exp = 0;
      this.postCount = 0;
      this.upvotesReceived = 0;
      this.downvotesReceived = 0;
      this.blockedUntil = DateTime.Now;
    }

    internal static void checkForTestingMode()
    {
      if (Utilities.Runningintestmode)
      {
        _cooldownminutes = 1;
        _cooldownminutesflagging = 1;
        _cooldownhoursposting = 0;
        _cooldownhoursfeedback = 0;
      }
    }

    internal bool canVote(long postId)
    {
      return !this.votedPosts.Contains(postId);
    }

    internal void vote(long postingId)
    {
      this.votedPosts.Add(postingId);
      this.lastActivity = DateTime.Now;
    }

    internal void flag(long postingId)
    {
      this.flaggedPosts.Add(postingId);
      this.lastActivity = DateTime.Now;
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
      lock (this.credibilityMutex)
      {
        this.downvotesReceived += receivedDownVotes;
        this.upvotesReceived += receivedUpVotes;
      }
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
      lock (this.credibilityMutex)
      {
        if(this.upvotesReceived + this.downvotesReceived != 0)
        {
          percentage = (int) ((this.upvotesReceived / (float) (this.upvotesReceived + this.downvotesReceived)) * 100.0f);
        }
      }
      return "Level " + this.getLevel() + " Schreiber/in mit " + percentage + " Prozent Zustimmung";
    }

    internal void publishedSuccessfully()
    {
      this.exp += ExpForPosting;
      this.postCount += 1;
      this.lastActivity = DateTime.Now;
    }

    internal string getAuthorName()
    {
      if (this.authorName == null)
      {
        return "";
      }
      return this.authorName;
    }

    public void setAuthorName(string externalName)
    {
      if (externalName == null)
      {
        this.authorName = "";
        return;
      }
      this.authorName = externalName;
    }

    internal long getAuthorId()
    {
      return this.authorId;
    }

    internal DateTime getLastActivity()
    {
      return this.lastActivity;
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
          cooldownTs = this.blockedUntil > this.coolDownTimeStampPosting ? this.blockedUntil : this.coolDownTimeStampPosting;
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
          cooldownTs = this.blockedUntil > this.coolDownTimeStampPosting ? this.blockedUntil : this.coolDownTimeStampPosting;
          break;
      }
      return cooldownTs.Subtract(DateTime.Now);
    }

    internal void resetCoolDown(InteractionCooldownTimer timerType)
    {
      switch (timerType)
      {
        case InteractionCooldownTimer.Feedback:
          this.coolDownTimeStampFeedback = DateTime.Now.AddHours(_cooldownhoursfeedback);
          break;
        case InteractionCooldownTimer.Flagging:
          this.coolDownTimeStampFlagging = DateTime.Now.AddMinutes(_cooldownminutesflagging);
          break;
        case InteractionCooldownTimer.Posting:
          this.coolDownTimeStampPosting = DateTime.Now.AddHours(_cooldownhoursposting);
          break;
        case InteractionCooldownTimer.Default:
          this.coolDownTimeStamp = DateTime.Now.AddMinutes(_cooldownminutes);
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
      lock (votingGaugeMutex)
      {
        this.exp += ExpForVote;
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
    }

    internal int voteDownAndGetCount()
    {
      lock (votingGaugeMutex)
      {
        this.exp += ExpForVote;
        if (this.votingGauge >= 0)
        {
          this.votingGauge -= 1;
          return 10;
        }
        else
        {
          int result = Math.Max(10 + this.votingGauge, 1);
          this.votingGauge -= 1;
          if (this.votingGauge < -10)
          {
            this.votingGauge = -10;
          }
          return result;
        }
      }
    }


    public void blockForDays(int days)
    {
      this.blockedUntil = DateTime.Now.AddDays(days);
    }
  }
    
}
