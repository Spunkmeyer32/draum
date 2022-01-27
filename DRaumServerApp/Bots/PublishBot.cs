using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DRaumServerApp.Bots
{
  public class PublishBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly TelegramBotClient telegramPublishBot;
    private readonly PostingManager posts;
    private readonly long draumChatId;
    private readonly long draumDailyChatId;
    private readonly long draumWeeklyChatId;
    private readonly PostingTextBuilder textBuilder;

    private CancellationTokenSource cts = new CancellationTokenSource();

    internal PublishBot(TelegramBotClient telegramPublishBot, PostingManager posts, PostingTextBuilder textBuilder, 
      Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
      Func<ITelegramBotClient, Exception, CancellationToken, Task> errorHandler)
    {
      this.draumChatId = long.Parse(ConfigurationManager.AppSettings["mainRoomID"]);
      this.draumDailyChatId = long.Parse(ConfigurationManager.AppSettings["dailyRoomID"]);
      this.draumWeeklyChatId = long.Parse(ConfigurationManager.AppSettings["weeklyRoomID"]);
      this.telegramPublishBot = telegramPublishBot;
      this.textBuilder = textBuilder;
      this.posts = posts;

      var receiverOptions = new ReceiverOptions();
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] { Telegram.Bot.Types.Enums.UpdateType.CallbackQuery };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramPublishBot.StartReceiving(
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
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] { Telegram.Bot.Types.Enums.UpdateType.CallbackQuery };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramPublishBot.StartReceiving(
        updateHandler,
        errorHandler,
        receiverOptions,
        cancellationToken: cts.Token);
    }


    internal async Task updatePostButtons(long postId, CancellationToken cancellationToken)
    {
      logger.Info("Buttons eines Posts (" + postId + ") werden aktualisiert");
      try
      {
        await this.telegramPublishBot.EditMessageReplyMarkupAsync(
          chatId: this.draumChatId,
          messageId: this.posts.getMessageId(postId),
          replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postId), this.posts.getDownVotes(postId), postId), 
          cancellationToken: cancellationToken).ConfigureAwait(false);
        this.posts.resetDirtyFlag(postId);
      }
      catch (OperationCanceledException)
      {
        // get out of loop
      }
      catch (Exception ex)
      {
        /// TODO Migrate!
        /*if (ex is  MessageIsNotModifiedException)
        {
          this.posts.resetDirtyFlag(postId);
          logger.Warn("Die Buttons des Posts " + postId + " waren nicht verändert");
        }
        else*/
        {
          logger.Error(ex, "Beim aktualisieren eines Buttons eines Beitrags (" + postId + ") trat ein Fehler auf: " + ex.Message);
        }
      }
    }

    internal async Task updatePostText(long postId)
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
        /// TODO Migrate!
        /*if (ex is MessageIsNotModifiedException)
        {
          this.posts.resetTextDirtyFlag(postId);
          logger.Warn("Der Text des Posts " + postId + " ist nicht verändert");
        }
        else */
        {
          logger.Error(ex, "Beim aktualisieren eines Textes eines Beitrags (" + postId + ") trat ein Fehler auf: " + ex.Message);
        }
      }
    }


    internal async Task publishInMainChannel(long postingId)
    {
      if (postingId != 0)
      {
        // Ab in den D-Raum damit
        logger.Info("Es soll folgender Post veröffentlicht werden: " + postingId);
        try
        {
          this.posts.setPublishTimestamp(postingId);
          Message result = await this.telegramPublishBot.SendTextMessageAsync(
            chatId: this.draumChatId,
            parseMode: ParseMode.Html,
            text: this.textBuilder.buildPostingText(postingId),
            disableWebPagePreview: true,
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postingId), this.posts.getDownVotes(postingId),
              postingId)
          );
          if (result == null || result.MessageId == 0)
          {
            logger.Error("Fehler beim Publizieren des Posts (keine msg ID) bei Post " + postingId + " wird neu eingereiht.");
            this.posts.reAcceptFailedPost(postingId);
            this.posts.unsetPublishTimestamp(postingId);
          }
          else
          {
            this.posts.resetTextDirtyFlag(postingId);
            this.posts.resetDirtyFlag(postingId);
            this.posts.setChatMsgId(postingId, result.MessageId);
            
          }
        }
        catch (Exception ex)
        {
          logger.Error(ex, "Fehler beim Veröffentlichen eines Posts im D-Raum, PostingId: " + postingId + " wird neu eingereiht.");
          this.posts.reAcceptFailedPost(postingId);
        }
      }
    }

    internal async Task publishInWeekly(long postId)
    {
      Message result;
      logger.Info("Es soll folgender Post in Top-Weekly veröffentlicht werden: " + postId);
      try
      {
        result = await this.telegramPublishBot.SendTextMessageAsync(
          chatId: this.draumWeeklyChatId,
          parseMode: ParseMode.Html,
          disableWebPagePreview: true,
          text: this.textBuilder.buildPostingTextForTopTeaser(postId),
          replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageId(postId), DRaumManager.Roomname)
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Publizieren im Weekly-Kanal :" + postId);
        result = null;
      }
      if (result == null || result.MessageId == 0)
      {
        logger.Error("Ergebnis ist null oder keine Msg-ID für das Posting erhalten " + postId);
      }
      else
      {
        this.posts.setWeeklyChatMsgId(postId, result.MessageId);
      }
    }

    internal async Task publishInDaily(long postId)
    {
      Message result;
      try
      {
        logger.Info("Es soll folgender Post in Top-Daily veröffentlicht werden: " + postId);
        result = await this.telegramPublishBot.SendTextMessageAsync(
          chatId: this.draumDailyChatId,
          parseMode: ParseMode.Html,
          disableWebPagePreview: true,
          text: this.textBuilder.buildPostingTextForTopTeaser(postId),
          replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageId(postId), DRaumManager.Roomname)
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Publizieren des Posts im Daily-Kanal: " + postId);
        result = null;
      }
      if (result == null || result.MessageId == 0)
      {
        logger.Error("Ergebnis ist null oder keine Msg-ID für das Posting erhalten " + postId);
      }
      else
      {
        this.posts.setDailyChatMsgId(postId, result.MessageId);
      }
    }

    internal async Task publishSilentlyAsHtml(string message)
    {
      try
      {
        await this.telegramPublishBot.SendTextMessageAsync(
          chatId: this.draumChatId,
          parseMode: ParseMode.Html,
          disableNotification: true,
          disableWebPagePreview: true,
          text: message).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim stillen Veröffentlichen");
      }
    }



    internal async Task answerCallback(string callbackId, string message)
    {
      try
      {
        await this.telegramPublishBot.AnswerCallbackQueryAsync(
          callbackQueryId: callbackId,
          text: message,
          showAlert: true
        ).ConfigureAwait(false);
      }
      catch (Exception e)
      {
        logger.Error(e, "Fehler beim Beantworten eines Callbacks im Publish-Bot");
      }
    }



  }
}