using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Bots;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace DRaumServerApp.CyclicTasks
{
  internal class VoteAndFlagTask
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly int intervalVoteAndFlagCountMinutes = 5;
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task voteAndFlagTask;

    private readonly PostingManager posts;
    private readonly long draumChatId;
    private readonly PostingTextBuilder textBuilder;
    private readonly TelegramBotClient telegramPublishBot;
    private readonly DRaumStatistics statistics;
    private readonly long adminChatId;
    private readonly AdminBot adminBot;

    private readonly HashSet<long> flaggedPostsSent = new HashSet<long>();

    internal VoteAndFlagTask(PostingManager postingManager, long draumChatId, 
      PostingTextBuilder textBuilder,TelegramBotClient telegramPublishBot, DRaumStatistics statistics, long adminChat, AdminBot adminBot)
    {
      this.statistics = statistics;
      this.adminChatId = adminChat;
      this.adminBot = adminBot;
      this.telegramPublishBot = telegramPublishBot;
      this.textBuilder = textBuilder;
      this.draumChatId = draumChatId;
      this.posts = postingManager;
      this.voteAndFlagTask = this.periodicVoteAndFlagTask(new TimeSpan(0, 0, intervalVoteAndFlagCountMinutes, 0, 0), this.cancelTaskSource.Token);
    }


    internal async Task shutDownTask()
    {
      this.cancelTaskSource.Cancel();
      try
      {
        await this.voteAndFlagTask;
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


    private async Task periodicVoteAndFlagTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Vote-And-Flag-Task ist gestartet");
      SyncManager.register();
      while (true)
      {
        SyncManager.tryRun(cancellationToken);
        try
        {
          await Task.Delay(interval, cancellationToken);
          await this.processVoteAndFlag(cancellationToken);
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
      logger.Info("Vote-And-Flag-Task ist beendet");

    }

    private async Task updatePostText(long postId)
    {
      try
      {
        await this.telegramPublishBot.EditMessageTextAsync(
          chatId: this.draumChatId,
          disableWebPagePreview: true,
          parseMode: ParseMode.Html,
          replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postId), this.posts.getDownVotes(postId), postId),
          messageId: this.posts.getMessageId(postId),
          text: this.textBuilder.buildPostingText(postId));
        this.posts.resetTextDirtyFlag(postId);
      }
      catch (Exception ex)
      {
        if (ex is MessageIsNotModifiedException)
        {
          this.posts.resetTextDirtyFlag(postId);
          logger.Warn("Der Text des Posts " + postId + " ist nicht verändert");
        }
        else
        {
          logger.Error(ex, "Beim aktualisieren eines Textes eines Beitrags (" + postId + ") trat ein Fehler auf.");
        }
      }
    }
    
    private async Task processVoteAndFlag(CancellationToken cancellationToken)
    {
      // Posts prüfen, ob Buttons im Chat angepasst werden müssen
      IEnumerable<long> dirtyposts = this.posts.getDirtyPosts();
      foreach (long postId in dirtyposts)
      {
        logger.Info("Buttons eines Posts (" + postId + ") werden aktualisiert");
        try
        {
          await this.telegramPublishBot.EditMessageReplyMarkupAsync(
            chatId: this.draumChatId,
            messageId: this.posts.getMessageId(postId),
            replyMarkup: TelegramUtilities.Keyboards.getPostKeyboard(this.posts.getUpVotes(postId), this.posts.getDownVotes(postId), postId), cancellationToken: cancellationToken);
          this.posts.resetDirtyFlag(postId);
          await Task.Delay(3000, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          return;
        }
        catch (Exception ex)
        {
          if (ex is MessageIsNotModifiedException)
          {
            this.posts.resetDirtyFlag(postId);
            logger.Warn("Die Buttons des Posts " + postId + " waren nicht verändert");
          }
          else
          {
            logger.Error(ex, "Beim aktualisieren eines Buttons eines Beitrags (" + postId + ") trat ein Fehler auf.");
          }
        }
        if (cancellationToken.IsCancellationRequested)
        {
          return;
        }
      }

      // Posts prüfen, ob Texte im Chat angepasst werden müssen
      dirtyposts = this.posts.getTextDirtyPosts();
      foreach (long postId in dirtyposts)
      {
        logger.Info("Text eines Posts ("+postId+") wird aktualisiert");
        await this.updatePostText(postId);
        try
        {
          await Task.Delay(3000, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          return;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          return;
        }
      }



      // Prüfen, ob ein Flag vorliegt und dem Admin melden
      IEnumerable<long> flaggedPosts = this.posts.getFlaggedPosts();
      HashSet<long> flaggedSentOld = new HashSet<long>();
      foreach (long postId in this.flaggedPostsSent)
      {
        flaggedSentOld.Add(postId);
      }
      this.flaggedPostsSent.Clear();
      foreach (long postId in flaggedPosts)
      {
        if (!flaggedSentOld.Contains(postId))
        {
          // getText and send to Admin
          string msgText = "Dieser Post wurde " + this.posts.getFlagCountOfPost(postId) + "-Mal geflaggt!!! \r\n" + this.posts.getPostingText(postId);
          Message msg = await this.adminBot.sendMessageWithKeyboard(this.adminChatId, msgText, TelegramUtilities.Keyboards.getFlaggedPostModKeyboard(postId));
          if (msg == null)
          {
            logger.Error("Beim senden eines geflaggten Beitrags trat ein Fehler auf.");
          }
          else
          {
            this.flaggedPostsSent.Add(postId);
          }
        }
        else
        {
          this.flaggedPostsSent.Add(postId);
        }
      }

      // Fehlermeldungen an den Admin
      await this.adminBot.handleErrorMemory(this.adminChatId, cancellationToken);


      // Update the Status of top-posts
      this.posts.updateTopPostStatus(this.statistics);
    }


  }
}