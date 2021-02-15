﻿using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.telegram
{
  internal class InputBot
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly AuthorManager authors;
    private readonly TelegramBotClient telegramInputBot;
    private readonly DRaumStatistics statistics;
    private readonly PostingManager posts;
    private readonly FeedbackManager feedbackManager;

    internal InputBot(AuthorManager authors, DRaumStatistics statistics, TelegramBotClient telegramInputBot, PostingManager posts, FeedbackManager feedbackManager)
    {
      this.authors = authors;
      this.statistics = statistics;
      this.telegramInputBot = telegramInputBot;
      this.posts = posts;
      this.feedbackManager = feedbackManager;
    }

    internal async Task sendMessage(long authorId, string message)
    {
      try
      {
        await this.telegramInputBot.SendTextMessageAsync(
          chatId: authorId,
          text: message
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht an den Autor " + authorId);
      }
    }

    internal async Task sendMessageWithKeyboard(long authorId, string message, InlineKeyboardMarkup keyboard)
    {
      try
      {
        await this.telegramInputBot.SendTextMessageAsync(
          chatId: authorId,
          text: message,
          replyMarkup: keyboard
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) an den Autor " + authorId);
      }
    }

    internal async Task switchToWriteMode(long authorid, string authorname, long chatid)
    {
      try
      {
        if (!this.authors.isCoolDownOver(authorid, authorname, Author.InteractionCooldownTimer.POSTING))
        {
          TimeSpan coolDownTime =
            this.authors.getCoolDownTimer(authorid, authorname, Author.InteractionCooldownTimer.POSTING);
          string msgCoolDownText = "(Spamvermeidung) Zeit bis zum nächsten Posting: " +
                                   coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
          if (coolDownTime.TotalMinutes > 180)
          {
            msgCoolDownText = "(Spamvermeidung) Zeit bis zum nächsten Posting: " +
                              coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
          }

          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatid,
            text: msgCoolDownText
          );
          return;
        }

        this.statistics.increaseInteraction();
        this.authors.setPostMode(authorid, authorname);
        await this.telegramInputBot.SendTextMessageAsync(
          chatId: chatid,
          text: DRaumManager.postIntro
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Fehler beim Wechsel in den Schreib-Modus");
      }
    }

    internal async Task switchToFeedbackMode(long authorid, string authorname, long chatid)
    {
      try
      {
        if (!this.authors.isCoolDownOver(authorid, authorname, Author.InteractionCooldownTimer.FEEDBACK))
        {
          TimeSpan coolDownTime =
            this.authors.getCoolDownTimer(authorid, authorname, Author.InteractionCooldownTimer.FEEDBACK);
          string msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Feedbackmöglichkeit: " +
                                   coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
          if (coolDownTime.TotalMinutes > 180)
          {
            msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Feedbackmöglichkeit: " +
                              coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
          }

          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatid,
            text: msgCoolDownText
          );
          return;
        }

        this.statistics.increaseInteraction();
        this.authors.setFeedbackMode(authorid, authorname);
        await this.telegramInputBot.SendTextMessageAsync(
          chatId: chatid,
          text: DRaumManager.feedbackIntro
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Welchsel in den Feedback-Modus");
      }
    }

    internal async Task processTextInput(long authorid, string authorname, long chatid, string text)
    {
      try
      {
        if (this.authors.isPostMode(authorid, authorname))
        {
          // == NEW POST SUBMITTED ==
          if (!SpamFilter.checkPostInput(text, out string posttext, out string message))
          {
            // spamfilter hat zugeschlagen
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: chatid,
              text: "Abgelehnt, Text ändern und erneut senden. Meldung des Spamfilters: " + message
            );
            return;
          }

          this.statistics.increaseInteraction();
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.POSTING);
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.DEFAULT);
          this.authors.unsetModes(authorid, authorname);
          this.posts.addPosting(posttext, authorid);
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatid,
            text: DRaumManager.replyPost + "\r\nMeldung des Spamfilters: " + message
          );
          return;
        }

        if (this.authors.isFeedbackMode(authorid, authorname))
        {
          // == Feedback ==
          if (!SpamFilter.checkPostInput(text, out string feedbacktext, out string message))
          {
            // spamfilter hat zugeschlagen
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: chatid,
              text: "Abgelehnt, Text ändern und erneut senden. Meldung des Spamfilters: " + message
            );
            return;
          }

          this.statistics.increaseInteraction();
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.FEEDBACK);
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.DEFAULT);
          this.authors.unsetModes(authorid, authorname);
          this.feedbackManager.enqueueFeedback(
            new FeedbackElement("Von: @" + authorname + " ID(" + authorid + ") : " + feedbacktext, chatid));
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatid,
            text: DRaumManager.replyFeedback
          );
          return;
        }

        // Kein Modus
        await this.telegramInputBot.SendTextMessageAsync(
          chatId: chatid,
          text: DRaumManager.noModeChosen,
          replyMarkup: Keyboards.getChooseInputModeKeyboard()
        );
      }
      catch(Exception ex)
      {
        logger.Error(ex, "Fehler beim Text-Verarbeiten");
      }
    }
    
  }
}