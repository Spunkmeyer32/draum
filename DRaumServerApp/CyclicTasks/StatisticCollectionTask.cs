using System;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Authors;
using DRaumServerApp.Bots;
using DRaumServerApp.Postings;
using Telegram.Bot.Types;

namespace DRaumServerApp.CyclicTasks
{
  internal class StatisticCollectionTask
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly int intervalStatisticCollectionMinutes = 60;
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task statisticCollectionTask;

    private readonly AuthorManager authors;
    private readonly DRaumStatistics statistics;
    private readonly PostingManager posts;
    private readonly AdminBot adminBot;

    private string adminStatisticText = "";
    private int adminStatisticMessageId = -1;

    internal StatisticCollectionTask(AuthorManager authors,DRaumStatistics statistics,PostingManager posts,AdminBot adminBot)
    {
      this.authors = authors;
      this.posts = posts;
      this.statistics = statistics;
      this.adminBot = adminBot;
      this.statisticCollectionTask = this.periodicStatisticCollectionTask(new TimeSpan(0, 0, intervalStatisticCollectionMinutes, 0, 0), this.cancelTaskSource.Token);
    }

    internal async Task shutDownTask()
    {
      this.cancelTaskSource.Cancel();
      try
      {
        await this.statisticCollectionTask;
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

    private async Task periodicStatisticCollectionTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Statistic-Collection-Task ist gestartet");
      SyncManager.register();
      while (true)
      {
        SyncManager.tryRun(cancellationToken);
        try
        {
          await Task.Delay(interval, cancellationToken);
          await this.processStatisticCollection();
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
      logger.Info("Statistic-Collection-Task ist beendet");
    }

    private async Task checkAndUpdateAdminStatistic()
    {
      string newtext = "Interaktionen im letzten Intervall: " + this.statistics.getLastInteractionIntervalCount()+"\r\n\r\n";
      newtext += "Letztes Backup: " + this.statistics.getLastBackup().ToString(Utilities.UsedCultureInfo)+"\r\n\r\n";
      newtext += "Hardware-Information:\r\n" + this.statistics.getHardwareInfo() + "\r\n\r\n";
      newtext += "Median-Votes: " + this.statistics.getMedianVotesPerPost() + "\r\n\r\n";
      newtext += "Interaktive Nutzer: " + this.authors.getAuthorCount();
      // structured logging
      logger.Debug("{@interactions} ; {@medianvotes} ; {@users}",
        this.statistics.getLastInteractionIntervalCount(),
        this.statistics.getMedianVotesPerPost(),
        this.authors.getAuthorCount());
      if (!newtext.Equals(this.adminStatisticText))
      {
        this.adminStatisticText = newtext;
        if (adminStatisticMessageId == -1)
        {
          Message msg = await this.adminBot.sendMessage(this.adminStatisticText);
          if (msg == null)
          {
            adminStatisticMessageId = -1;
            logger.Error("Fehler beim Senden der Statistiknachricht an den Admin");
          }
          else
          {
            adminStatisticMessageId = msg.MessageId;
          }
        }
        else
        {
          await this.adminBot.editMessage(adminStatisticMessageId, this.adminStatisticText);
        }
      }
    }

    private async Task processStatisticCollection()
    {
      try
      {
        this.statistics.switchInteractionInterval();
        await this.checkAndUpdateAdminStatistic();
        this.authors.getMedianAndTopLevel(out var median, out var top);
        this.statistics.updateWritersLevel(top,median);
        long medianVotes = this.posts.getMedianVotes();
        this.statistics.setVotesMedian(medianVotes);
        logger.Info("Statisktik ist nun aktualisiert");
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Verarbeiten der Statistik");
      }
    }

  }
}