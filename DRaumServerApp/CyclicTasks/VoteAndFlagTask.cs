using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Bots;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;
using Telegram.Bot.Types;


namespace DRaumServerApp.CyclicTasks
{
  internal class VoteAndFlagTask
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly int intervalVoteAndFlagCountMinutes = 5;
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task voteAndFlagTask;

    private readonly PostingManager posts;
    private readonly PublishBot publishBot;
    private readonly DRaumStatistics statistics;
    private readonly AdminBot adminBot;

    private readonly HashSet<long> flaggedPostsSent = new HashSet<long>();

    internal VoteAndFlagTask(PostingManager postingManager,PublishBot publishBot, DRaumStatistics statistics, AdminBot adminBot)
    {
      this.statistics = statistics;
      this.adminBot = adminBot;
      this.publishBot = publishBot;
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
        try
        {
          await SyncManager.tryRunAfter(interval,"voteandflag",cancellationToken);
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

    private async Task checkDirtyPosts(CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return;
      }
      // Posts prüfen, ob Buttons im Chat angepasst werden müssen
      IEnumerable<long> dirtyposts = this.posts.getDirtyPosts();
      foreach (long postId in dirtyposts)
      {
        try
        {
          await this.publishBot.updatePostButtons(postId, cancellationToken);
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
    }

    private async Task checkDirtyTextPosts(CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return;
      }
      // Posts prüfen, ob Texte im Chat angepasst werden müssen
      IEnumerable<long> dirtyposts = this.posts.getTextDirtyPosts();
      foreach (long postId in dirtyposts)
      {
        logger.Info("Text eines Posts ("+postId+") wird aktualisiert");
        await this.publishBot.updatePostText(postId);
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
    }

    private async Task checkAndSendFlaggedPosts(CancellationToken cancellationToken)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return;
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
          Message msg = await this.adminBot.sendMessageWithKeyboard(msgText, Keyboards.getFlaggedPostModKeyboard(postId));
          if (msg == null)
          {
            logger.Error("Beim senden eines geflaggten Beitrags trat ein Fehler auf.");
          }
          else
          {
            this.flaggedPostsSent.Add(postId);
          }
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
        else
        {
          this.flaggedPostsSent.Add(postId);
        }
      }
    }


    private async Task processVoteAndFlag(CancellationToken cancellationToken)
    {
      await checkDirtyPosts(cancellationToken);
      await checkDirtyTextPosts(cancellationToken);
      await checkAndSendFlaggedPosts(cancellationToken);
      // Fehlermeldungen an den Admin
      await this.adminBot.handleErrorMemory(cancellationToken);
      this.posts.updateTopPostStatus(this.statistics);
    }


  }
}