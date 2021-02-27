using System;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Bots;
using DRaumServerApp.Postings;
using Telegram.Bot.Types;

namespace DRaumServerApp.CyclicTasks
{
  internal class ModerationCheckTask
  {

    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly int intervalModerationCheckMilliseconds = 500;
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task moderationCheckTask;

    private readonly PostingManager posts;
    private readonly FeedbackManager feedbackManager;
    private readonly ModerateBot moderateBot;


    internal ModerationCheckTask(PostingManager posts,FeedbackManager feedbackManager,ModerateBot moderateBot)
    {
      this.posts = posts;
      this.feedbackManager = feedbackManager;
      this.moderateBot = moderateBot;
      this.moderationCheckTask = this.periodicModerationCheckTask(new TimeSpan(0, 0, 0, 0, intervalModerationCheckMilliseconds), this.cancelTaskSource.Token);
    }

    internal async Task shutDownTask()
    {
      this.cancelTaskSource.Cancel();
      try
      {
        await this.moderationCheckTask;
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

    private async Task periodicModerationCheckTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Moderation-Prüfen-Task ist gestartet");
      SyncManager.register();
      while (true)
      {
        try
        {
          await SyncManager.tryRunAfter(interval,"moderationcheck",cancellationToken);
          await this.processModerationCheckTask();
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
      logger.Info("Moderation-Prüfen-Task ist beendet");
    }
    

    private async Task processModerationCheckTask()
    {
      // Eingehende, zu moderierende Posts bearbeiten
      if (this.posts.getAndResetPostsCheckChangeFlag())
      {
        int messageId = this.feedbackManager.getModerateMessageId();
        int postsToCheck = this.posts.howManyPostsToCheck();
        string message = "Es gibt " + postsToCheck + " Posts zu moderieren.";
        if (messageId == -1)
        {
          Message msg = await this.moderateBot.sendMessageWithKeyboard(message,
            TelegramUtilities.Keyboards.getGetNextPostToModerateKeyboard(),false);
          if (msg == null)
          {
            logger.Error("Fehler beim Anlegen der Moderations-Nachricht");
          }
          else
          {
            this.feedbackManager.setModerateMessageId(msg.MessageId);
          }
        }
        else
        {
          // Update der Message
          await this.moderateBot.editMessage(messageId, message,
            TelegramUtilities.Keyboards.getGetNextPostToModerateKeyboard());
        }
      }
    }

    
  }
}