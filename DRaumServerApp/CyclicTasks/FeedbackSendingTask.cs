using System;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.TelegramUtilities;
using Telegram.Bot.Types;

namespace DRaumServerApp.CyclicTasks
{
  internal class FeedbackSendingTask
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private const int IntervalSendFeedbackSeconds = 30;
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task feedbackSendingTask;

    private readonly FeedbackManager feedbackManager;
    private readonly Bots.FeedbackBot feedbackBot;

    internal FeedbackSendingTask(FeedbackManager feedbackManager, Bots.FeedbackBot feedbackBot)
    {
      this.feedbackManager = feedbackManager;
      this.feedbackBot = feedbackBot;
      this.feedbackSendingTask = this.periodicFeedbackSendingTask(new TimeSpan(0, 0, 0, IntervalSendFeedbackSeconds, 0), this.cancelTaskSource.Token);
    }

    internal async Task shutDownTask()
    {
      this.cancelTaskSource.Cancel();
      try
      {
        await this.feedbackSendingTask;
      }
      catch (OperationCanceledException e)
      {
        logger.Warn($"{nameof(OperationCanceledException)} erhalten mit der Nachricht: {e.Message}");
      }
      finally
      {
        this.cancelTaskSource.Dispose();
      }
    }

    private async Task periodicFeedbackSendingTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Feedback-Senden-Task ist gestartet");
      SyncManager.register();
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
          SyncManager.tryRun(cancellationToken);
          await this.processFeedback();
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }
      }
      SyncManager.unregister();
      logger.Info("Feedback-Senden-Task ist beendet");
    }

    private async Task processFeedback()
    {
      if (!this.feedbackManager.feedBackAvailable() || this.feedbackManager.isWaitingForFeedbackReply())
      {
        return;
      }
      // erhaltene Feedbacks verarbeiten, wenn grad keine Antwort geschrieben wird
      FeedbackElement feedback = this.feedbackManager.dequeueFeedback();
      if (feedback != null)
      {
        Message msg = await this.feedbackBot.sendMessageWithKeyboard(feedback.Text,
          Keyboards.getFeedbackReplyKeyboard(feedback.ChatId));
        if (msg == null || msg.MessageId == 0)
        {
          logger.Error(
            "Es gab ein Problem beim senden der Feedback-Nachricht. Feedback wird neu in die Liste einsortiert.");
          this.feedbackManager.enqueueFeedback(feedback);
        }
      }
    }

  }
}