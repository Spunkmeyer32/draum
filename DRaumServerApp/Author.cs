using Newtonsoft.Json;
using System;

namespace DRaumServerApp
{
  class Author
  {

    public enum InteractionCooldownTimer { NONE, DEFAULT, POSTING, FLAGGING, FEEDBACK };

    private static int COOLDOWNMINUTES = 2;
    private static int COOLDOWNMINUTESFLAGGING = 30; 
    private static int COOLDOWNHOURSPOSTING = 3;
    private static int COOLDOWNHOURSFEEDBACK = 1;
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
    private long authorID;
    [JsonProperty]
    private String externalUserName;
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

    internal Author()
    {
      this.loadDefaults();
      this.coolDownTimeStamp = DateTime.Now;
    }

    internal Author(long authorID, String externalUserName)
    {
      this.loadDefaults();
      this.authorID = authorID;
      this.externalUserName = externalUserName;     
    }

    private void loadDefaults()
    {
      this.authorID = -1;
      this.externalUserName = "";
      this.postmode = false;
      this.feedbackmode = false;
      this.coolDownTimeStamp = DateTime.Now;
      this.votingGauge = 0;
      this.level = 1;
      this.exp = 0;
      this.postCount = 0;
      this.upvotesReceived = 0;
      this.downvotesReceived = 0;
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

    public int getUserLevel()
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

    internal String getUserInfo()
    {
      int percentage = 50;
      if(this.getTotalVotes() != 0)
      {
        percentage = (int)((this.upvotesReceived / (float)this.getTotalVotes()) * 100.0f);
      }
      return "Level " + this.getUserLevel() + " Schreiber/in mit " + percentage + " Prozent  Zustimmung";
    }

    internal String getFullUserInfo()
    {
      return "@"+this.externalUserName + " ("+this.authorID+")\r\n" + this.getUserInfo();
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

    internal String getExternalUserName()
    {
      return this.externalUserName;
    }

    internal long getAuthorID()
    {
      return this.authorID;
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
      if (votingGauge <= 0)
      {
        votingGauge += 1;
        return 10;
      }
      else
      {
        int result = Math.Max(10 - (votingGauge / 2), 5);
        votingGauge += 1;
        if (votingGauge > 10)
        {
          votingGauge = 10;
        }
        return result;
      }
    }

    internal int voteDownAndGetCount()
    {
      this.exp += EXP_FOR_VOTE;
      if (votingGauge >= 0)
      {
        votingGauge -= 1;
        return 10;
      }
      else
      {
        int result = Math.Max(10 + (votingGauge), 1);
        votingGauge -= 1;
        if (votingGauge < -10)
        {
          votingGauge = -10;
        }
        return result;
      }
    }

  }
    
}
