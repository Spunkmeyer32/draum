using Newtonsoft.Json;
using System.Collections.Concurrent;
using NLog;


namespace DRaumServerApp
{
  class FeedbackManager
  {
    [JsonIgnore]
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    [JsonIgnore]
    private readonly object dataMutex = new object();
    [JsonIgnore]
    private readonly object dataMutexFeedback = new object();
    [JsonIgnore]
    private int moderateMessageId = -1;
  

    

    [JsonProperty]
    private ConcurrentQueue<FeedbackElement> feedbacks;
    [JsonProperty]
    private bool waitForModeratedText;
    [JsonProperty]
    private bool waitForDenyText;
    [JsonProperty]
    private long nextPostModerationId;
    [JsonProperty]
    private bool waitForFeedbackReply;
    [JsonProperty]
    private long nextChatIdForFeedback;

    internal FeedbackManager()
    {
      this.feedbacks = new ConcurrentQueue<FeedbackElement>();
      lock (this.dataMutex)
      {
        this.waitForDenyText = false;
        this.waitForModeratedText = false;
        this.nextPostModerationId = 0;
      }      
    }

    internal void enqueueFeedback(FeedbackElement feedback)
    {
      this.feedbacks.Enqueue(feedback);
    }

    internal FeedbackElement dequeueFeedback()
    {
      if (this.feedbacks.TryDequeue(out FeedbackElement result))
      {
        return result;
      }
      logger.Warn("Konnte kein Feedback-Element laden");
      return null;
    }

    internal bool feedBackAvailable()
    {
      return (this.feedbacks.Count != 0);
    }

    internal int getModerateMessageId()
    {
      return this.moderateMessageId;
    }

    internal void setModerateMessageId(int messageId)
    {
      this.moderateMessageId = messageId;
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
        this.nextChatIdForFeedback = chatid;
      }
    }

    internal long processFeedbackReplyAndGetChatId()
    {
      lock(this.dataMutexFeedback)
      {
        this.waitForFeedbackReply = false;
        return this.nextChatIdForFeedback;
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

    internal long getNextModeratedPostId()
    {
      lock(this.dataMutex)
      {
        return this.nextPostModerationId;
      }
    }

    internal void resetProcessModerationText()
    {
      lock (this.dataMutex)
      {
        this.waitForDenyText = false;
        this.waitForModeratedText = false;
        this.nextPostModerationId = -1;
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
