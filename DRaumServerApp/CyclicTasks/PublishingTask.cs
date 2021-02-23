﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
    private readonly PostingTextBuilder textBuilder;

    private const int IntervalCheckPublishSeconds = 60;
    private readonly CancellationTokenSource cancelTaskSource = new CancellationTokenSource();
    private readonly Task publishTask;

    private readonly TelegramBotClient telegramPublishBot;
    private readonly long draumChatId;
    private readonly long draumDailyChatId;
    private readonly long draumWeeklyChatId;

    internal PublishingTask(TelegramBotClient publishBot, long chatId, PostingManager postingManager, long chatIdDaily, long chatIdWeekly, PostingTextBuilder textBuilder)
    {
      this.textBuilder = textBuilder;
      this.draumDailyChatId = chatIdDaily;
      this.draumWeeklyChatId = chatIdWeekly;
      this.posts = postingManager;
      this.draumChatId = chatId;
      this.telegramPublishBot = publishBot;
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
        SyncManager.tryRun(cancellationToken);
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }
        await this.processPublishing();
      }
      SyncManager.unregister();
      logger.Info("Publishing-Task ist beendet");
    }

    private async Task processPublishing()
    {
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
          string motd = "== Service Post ==\r\n\r\n✍️ Möchten Sie selbst auch hier schreiben?\r\nDann verwenden Sie dazu den Eingabe-Bot:\r\n\r\n  🤖  @d_raum_input_bot  🤖";
          try
          {
            await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumChatId,
              disableNotification: true,
              disableWebPagePreview: true,
              text: motd);
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Posten der MOTD");
          }
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
          string news = this.worldInfoManager.getInfoStringForChat();
          try
          {
            await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumChatId,
              disableNotification: true,
              disableWebPagePreview: true,
              text: news);
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Posten der News");
          }
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
          List<long> topPosts = this.posts.getDailyTopPostsFromYesterday();
          foreach (long postId in topPosts)
          {
            logger.Info("Es soll folgender Post in Top-Daily veröffentlicht werden: " + postId);
            Message result = await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumDailyChatId,
              parseMode: ParseMode.Html,
              disableWebPagePreview: true,
              text: this.textBuilder.buildPostingTextForTopTeaser(postId),
              replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageId(postId), DRaumManager.Roomname)
            );
            if (result == null || result.MessageId == 0)
            {
              logger.Error("Fehler beim Publizieren des Posts (daily,keine msg ID) bei Post " + postId);
            }
            else
            {
              this.posts.setDailyChatMsgId(postId, result.MessageId);
            }
            await Task.Delay(3000);
          }

          List<long> deleteablePosts = this.posts.getPostsToDelete();
          foreach (long postId in deleteablePosts)
          {
            logger.Info("Es soll folgender Post gelöscht werden (abgelaufen): " + postId);
            long messageId = this.posts.getMessageId(postId);
            long messageDailyId = this.posts.getMessageIdDaily(postId);
            long messageWeeklyId = this.posts.getMessageIdWeekly(postId);
            if (messageId != -1)
            {
              try
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumChatId,
                  messageId: (int)messageId);
              }
              catch (Exception ex)
              {
                logger.Error(ex, "Fehler beim Löschen aus dem D-Raum");
              }
            }
            if (messageDailyId != -1)
            {
              try
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumDailyChatId,
                  messageId: (int)messageDailyId);
              }
              catch (Exception ex)
              {
                logger.Error(ex, "Fehler beim Löschen aus dem D-Raum-Täglich");
              }
            }
            if (messageWeeklyId != -1)
            {
              try
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumWeeklyChatId,
                  messageId: (int)messageWeeklyId);
              }
              catch (Exception ex)
              {
                logger.Error(ex, "Fehler beim Löschen aus dem D-Raum-Wöchentlich");
              }
            }
            this.posts.deletePost(postId);
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
            logger.Info("Es soll folgender Post in Top-Weekly veröffentlicht werden: " + postId);
            Message result = await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumWeeklyChatId,
              parseMode: ParseMode.Html,
              disableWebPagePreview: true,
              text: this.textBuilder.buildPostingTextForTopTeaser(postId),
              replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageId(postId), DRaumManager.Roomname)
            );
            if (result == null || result.MessageId == 0)
            {
              logger.Error("Fehler beim Publizieren des Posts (weekly,keine msg ID) bei Post " + postId);
            }
            else
            {
              this.posts.setWeeklyChatMsgId(postId, result.MessageId);
            }
            await Task.Delay(3000);
          }
        }
      }

      long postingId = -1;
      bool fail = false;
      try
      {
        Posting toPublish = this.posts.tryPublish();
        if (toPublish != null)
        {
          postingId = toPublish.getPostId();
          // Ab in den D-Raum damit
          logger.Info("Es soll folgender Post veröffentlicht werden: " + postingId);
          Message result = await this.telegramPublishBot.SendTextMessageAsync(
            chatId: this.draumChatId,
            parseMode: ParseMode.Html,
            text: this.textBuilder.buildPostingText(postingId),
            disableWebPagePreview: true,
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postingId), this.posts.getDownVotes(postingId), postingId)
          );
          if (result == null || result.MessageId == 0)
          {
            logger.Error("Fehler beim Publizieren des Posts (keine msg ID) bei Post " + postingId);
          }
          else
          {
            toPublish.resetTextDirtyFlag();
            toPublish.resetDirtyFlag();
            toPublish.setChatMessageId(result.MessageId);
          }
        }
      }
      catch (Exception e)
      {
        logger.Error(e, "(Exception)Fehler beim Publizieren des Posts: " + postingId);
        fail = true;
      }
      if (fail)
      {
        // TODO den Post wieder einreihen
      }
    }

  }
}