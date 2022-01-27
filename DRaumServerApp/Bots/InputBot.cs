using System;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Authors;
using DRaumServerApp.Postings;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class InputBot
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly AuthorManager authors;
    private readonly TelegramBotClient telegramInputBot;
    private readonly DRaumStatistics statistics;
    private readonly PostingManager posts;
    private readonly FeedbackManager feedbackManager;

    private CancellationTokenSource cts = new CancellationTokenSource();

    internal InputBot(AuthorManager authors, DRaumStatistics statistics, TelegramBotClient telegramInputBot, 
      PostingManager posts, FeedbackManager feedbackManager,
      Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
      Func<ITelegramBotClient, Exception, CancellationToken, Task> errorHandler)
    {
      this.authors = authors;
      this.statistics = statistics;
      this.telegramInputBot = telegramInputBot;
      this.posts = posts;
      this.feedbackManager = feedbackManager;

      var receiverOptions = new ReceiverOptions();
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] { 
        Telegram.Bot.Types.Enums.UpdateType.CallbackQuery,
        Telegram.Bot.Types.Enums.UpdateType.Message
      };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramInputBot.StartReceiving(
        updateHandler,
        errorHandler,
        receiverOptions,
        cancellationToken: cts.Token);

    }

    internal void stopListening()
    {
      this.cts.Cancel();
    }

    internal void restartListening(Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
      Func<ITelegramBotClient, Exception, CancellationToken, Task> errorHandler)
    {
      cts = new CancellationTokenSource();
      var receiverOptions = new ReceiverOptions();
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] {
        Telegram.Bot.Types.Enums.UpdateType.CallbackQuery,
        Telegram.Bot.Types.Enums.UpdateType.Message
      };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramInputBot.StartReceiving(
        updateHandler,
        errorHandler,
        receiverOptions,
        cancellationToken: cts.Token);
    }

    internal async Task removeMessage(int messageId, long authorId)
    {
      try
      {
        await this.telegramInputBot.DeleteMessageAsync(
          messageId: messageId,
          chatId: authorId
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim löschen einer Nachricht an den Autor " + authorId + " Msg-ID: " + messageId);
      }
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
        if (!this.authors.isCoolDownOver(authorid, authorname, Author.InteractionCooldownTimer.Posting))
        {
          TimeSpan coolDownTime =
            this.authors.getCoolDownTimer(authorid, authorname, Author.InteractionCooldownTimer.Posting);
          string msgCoolDownText = "⏳ (Spamvermeidung) Zeit bis zum nächsten Posting: " +
                                   coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
          if (coolDownTime.TotalHours > 24)
          {
            msgCoolDownText = "⏳ (Spamvermeidung) Zeit bis zum nächsten Posting: " +
                              coolDownTime.TotalDays.ToString("0.0") + " Tag(e)";
          }
          else
          {
            if (coolDownTime.TotalMinutes > 180)
            {
              msgCoolDownText = "⏳ (Spamvermeidung) Zeit bis zum nächsten Posting: " +
                                coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
            }
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
          text: DRaumManager.PostIntro
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex,"Fehler beim Wechsel in den Schreib-Modus");
      }
    }

    /// <summary>
    /// Wenn der Benutzer den Befehl für das Feedbackschreiben eingegeben hat
    /// </summary>
    /// <param name="authorid"></param>
    /// <param name="authorname"></param>
    /// <param name="chatid"></param>
    /// <returns></returns>
    internal async Task switchToFeedbackMode(long authorid, string authorname, long chatid)
    {
      try
      {
        if (!this.authors.isCoolDownOver(authorid, authorname, Author.InteractionCooldownTimer.Feedback))
        {
          TimeSpan coolDownTime =
            this.authors.getCoolDownTimer(authorid, authorname, Author.InteractionCooldownTimer.Feedback);
          string msgCoolDownText = "⏳ (Spamvermeidung) Zeit bis zur nächsten Feedbackmöglichkeit: " +
                                   coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
          if (coolDownTime.TotalMinutes > 180)
          {
            msgCoolDownText = "⏳ (Spamvermeidung) Zeit bis zur nächsten Feedbackmöglichkeit: " +
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
          text: DRaumManager.FeedbackIntro
        );
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Welchsel in den Feedback-Modus");
      }
    }

    /// <summary>
    /// Wird aufgerufen, wenn der Benutzer normalen Text (kein Kommando) eingegeben hat
    /// </summary>
    /// <param name="authorid"></param>
    /// <param name="authorname"></param>
    /// <param name="chatid"></param>
    /// <param name="text"></param>
    /// <returns></returns>
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
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.Posting);
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.Default);
          this.authors.unsetModes(authorid, authorname);
          this.posts.addPosting(posttext, authorid);
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatid,
            text: DRaumManager.ReplyPost + "\r\nMeldung des Spamfilters: " + message
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
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.Feedback);
          this.authors.resetCoolDown(authorid, authorname, Author.InteractionCooldownTimer.Default);
          this.authors.unsetModes(authorid, authorname);
          this.feedbackManager.enqueueFeedback(
            new FeedbackElement("Von: @" + authorname + " ID(" + authorid + ") : " + feedbacktext, chatid));
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatid,
            text: DRaumManager.ReplyFeedback
          );
        }
      }
      catch(Exception ex)
      {
        logger.Error(ex, "Fehler beim Text-Verarbeiten");
      }
    }

    
  }
}