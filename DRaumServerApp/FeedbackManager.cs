using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;


namespace DRaumServerApp
{
  class FeedbackManager
  {
    [JsonIgnore]
    private readonly object dataMutex = new object();
    [JsonIgnore]
    private readonly object dataMutexFeedback = new object();
    [JsonIgnore]
    private int moderateMessageID = -1;

    [JsonProperty]
    private ConcurrentQueue<FeedbackElement> feedBacks;
    [JsonProperty]
    private bool waitForModeratedText;
    [JsonProperty]
    private bool waitForDenyText;
    [JsonProperty]
    private long nextPostModerationId;
    [JsonProperty]
    private bool waitForFeedbackReply;
    [JsonProperty]
    private long nextChatIDForFeedback;

    internal FeedbackManager()
    {
      this.feedBacks = new ConcurrentQueue<FeedbackElement>();
      lock (this.dataMutex)
      {
        this.waitForDenyText = false;
        this.waitForModeratedText = false;
        this.nextPostModerationId = 0;
      }      
    }

    internal void enqueueFeedback(FeedbackElement feedback)
    {
      this.feedBacks.Enqueue(feedback);
    }

    internal FeedbackElement dequeueFeedback()
    {
      FeedbackElement result = new FeedbackElement();
      this.feedBacks.TryDequeue(out result);
      return result;
    }

    internal bool feedBackAvailable()
    {
      return (this.feedBacks.Count != 0);
    }

    internal int getModerateMessageID()
    {
      return this.moderateMessageID;
    }

    internal void setModerateMessageID(int messageID)
    {
      this.moderateMessageID = messageID;
    }

    internal void waitForModerationText(long id)
    {
      lock (this.dataMutex)
      {
        this.waitForModeratedText = true;
        this.waitForDenyText = false;
        this.nextPostModerationId = id;
      }
    }

    internal void enableWaitForFeedbackReply(long chatid)
    {
      lock(this.dataMutexFeedback)
      {
        this.waitForFeedbackReply = true;
        this.nextChatIDForFeedback = chatid;
      }
    }

    internal long processFeedbackReplyAndGetChatID()
    {
      lock(this.dataMutexFeedback)
      {
        this.waitForFeedbackReply = false;
        return this.nextChatIDForFeedback;
      }
    }

    internal void waitForDenyingText(long id)
    {
      lock (this.dataMutex)
      {
        this.waitForModeratedText = false;
        this.waitForDenyText = true;
        this.nextPostModerationId = id;
      }
    }

    internal long processModerationText()
    {
      lock (this.dataMutex)
      {
        this.waitForDenyText = false;
        this.waitForModeratedText = false;
        return this.nextPostModerationId;
      }
    }

    internal bool isWaitingForFeedbackReply()
    {
      return this.waitForFeedbackReply;
    }

    internal bool isWaitingForModeratedText()
    {
      return this.waitForModeratedText;
    }

    internal bool isWaitingForDenyText()
    {
      return this.waitForDenyText;
    }
  }
}
