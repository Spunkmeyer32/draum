﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Bots;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;


namespace DRaumServerApp.CyclicTasks
{
  internal class PublishingTask
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private DateTime lastWorldNewsPost = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastTopDaily = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastTopWeekly = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastMessageOfTheDay = new DateTime(1999, 1, 1, 9, 0, 0);

    private readonly WorldInfoManager worldInfoManager = new WorldInfoManager();
    private readonly PostingManager posts;

    private const int IntervalCheckPublishSeconds = 60;
    private const string MessageOfTheDay = "== Service Post ==\r\n\r\n✍️ Möchten Sie selbst auch hier schreiben?\r\nDann verwenden Sie dazu den Eingabe-Bot:\r\n\r\n  🤖  @d_raum_input_bot  🤖";
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task publishTask;
    private readonly PublishBot publishBot;
    private readonly AdminBot adminBot;

    private int lastSeenPostsToEdit = 0;

    internal PublishingTask(PublishBot publishBot,PostingManager postingManager, AdminBot adminBot)
    {
      this.posts = postingManager;
      this.publishBot = publishBot;
      this.adminBot = adminBot;
      this.publishTask = this.periodicPublishingTask(new TimeSpan(0, 0, 0, IntervalCheckPublishSeconds, 0), this.cancelTaskSource.Token);
    }

    internal async Task shutDownTask()
    {
      this.cancelTaskSource.Cancel();
      try
      {
        await this.publishTask;
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

    private async Task periodicPublishingTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Publishing-Task ist gestartet");
      SyncManager.register();
      while (true)
      {
        try
        {
          await SyncManager.tryRunAfter(interval,cancellationToken);
          await this.processPublishing();
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
      logger.Info("Publishing-Task ist beendet");
    }

    private async Task processPublishing()
    {
      int newPostsToEdit = this.posts.howManyPostsToCheck();
      if (lastSeenPostsToEdit != newPostsToEdit)
      {
        lastSeenPostsToEdit = newPostsToEdit;
        if (lastSeenPostsToEdit > 0)
        {
          await this.adminBot.sendMessage("Es sind Beiträge zum Moderieren vorhanden");
        }
      }
      bool skip = false;
      // Message Of The Day
      if ((DateTime.Now - this.lastMessageOfTheDay).TotalHours > 24.0)
      {
        if (this.lastMessageOfTheDay.Year <= 2000)
        {
          if (!Utilities.Runningintestmode)
          {
            skip = true;
          }
        }
        this.lastMessageOfTheDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 30, 0);
        logger.Info("Nächste MOTD am " + this.lastMessageOfTheDay.AddHours(24).ToString(Utilities.UsedCultureInfo));
        if (!skip)
        {
          await this.publishBot.publishSilentlyAsHtml(MessageOfTheDay);
        }
      }

      skip = false;
      // News-Post
      if ((DateTime.Now - this.lastWorldNewsPost).TotalHours > 24.0)
      {
        if (this.lastWorldNewsPost.Year <= 2000)
        {
          if (!Utilities.Runningintestmode)
          {
            skip = true;
          }
        }
        this.lastWorldNewsPost = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);

        logger.Info("Nächste News am " + this.lastWorldNewsPost.AddHours(24).ToString(Utilities.UsedCultureInfo));
        if (!skip)
        {
          await this.publishBot.publishSilentlyAsHtml(this.worldInfoManager.getInfoStringForChatAsHtml(true));
        }
      }

      skip = false;
      // Top-Daily
      if ((DateTime.Now - this.lastTopDaily).TotalHours > 24.0)
      {
        if (this.lastTopDaily.Year <= 2000)
        {
          if (!Utilities.Runningintestmode)
          {
            skip = true;
          }
        }
        this.lastTopDaily = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
        logger.Info("Nächste Top Tages Posts am " + this.lastTopDaily.AddHours(24).ToString(Utilities.UsedCultureInfo));
        if (!skip)
        {
          IEnumerable<long> topPosts = this.posts.getDailyTopPostsFromYesterday();
          foreach (long postId in topPosts)
          {
            await this.publishBot.publishInDaily(postId);
            await Task.Delay(3000);
          }
          IEnumerable<long> deleteablePosts = this.posts.getPostsToDelete();
          foreach (long postingID in deleteablePosts)
          {
            
            int messageId = this.posts.getMessageId(postingID);
            int messageIdDaily = this.posts.getMessageIdDaily(postingID);
            int messageIdWeekly = this.posts.getMessageIdWeekly(postingID);
            if (!this.posts.removePost(postingID))
            {
              logger.Error("Konnte den Post " + postingID + " nicht aus dem Datensatz löschen");
            }
            await this.adminBot.sendMessageWithKeyboard("Bitte manuell löschen",
              Keyboards.getPostLinkWithCustomText(messageId, DRaumManager.Roomname, "Link zum D-Raum"));
            if (messageIdDaily != -1)
            {
              await this.adminBot.sendMessageWithKeyboard("Bitte manuell löschen",
                Keyboards.getPostLinkWithCustomText(messageIdDaily, DRaumManager.Roomname_daily, "Link zum D-Raum-Daily"));
            }
            if (messageIdWeekly != -1)
            {
              await this.adminBot.sendMessageWithKeyboard("Bitte manuell löschen",
                Keyboards.getPostLinkWithCustomText(messageIdWeekly, DRaumManager.Roomname_weekly, "Link zum D-Raum-Daily"));
            }
            await Task.Delay(3000);
          }
        }
      }

      skip = false;
      // Top-Weekly
      if ((DateTime.Now - this.lastTopWeekly).TotalDays > 7.0)
      {
        DayOfWeek currentDay = DateTime.Now.DayOfWeek;
        int daysTillCurrentDay = currentDay - DayOfWeek.Saturday;
        if (daysTillCurrentDay < 0)
        {
          daysTillCurrentDay += 7;
        }

        DateTime currentWeekStartDate = DateTime.Now.AddDays(-daysTillCurrentDay);
        if (this.lastTopWeekly.Year <= 2000)
        {
          if (!Utilities.Runningintestmode)
          {
            skip = true;
          }
        }
        this.lastTopWeekly = new DateTime(currentWeekStartDate.Year, currentWeekStartDate.Month, currentWeekStartDate.Day, 9, 0, 0);
        logger.Info("Nächste Top Wochen Posts am " + this.lastTopWeekly.AddDays(7).ToString(Utilities.UsedCultureInfo));
        if (!skip)
        {
          List<long> topPosts = this.posts.getWeeklyTopPostsFromLastWeek();
          foreach (long postId in topPosts)
          {
            await this.publishBot.publishInWeekly(postId);
            await Task.Delay(3000);
          }
        }
      }

      await this.publishBot.publishInMainChannel(this.posts.tryPublish());
      
    }

  }
}