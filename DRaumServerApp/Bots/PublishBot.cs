using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
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

    internal PublishBot(TelegramBotClient telegramPublishBot, PostingManager posts, PostingTextBuilder textBuilder)
    {
      this.draumChatId = long.Parse(ConfigurationManager.AppSettings["mainRoomID"]);
      this.draumDailyChatId = long.Parse(ConfigurationManager.AppSettings["dailyRoomID"]);
      this.draumWeeklyChatId = long.Parse(ConfigurationManager.AppSettings["weeklyRoomID"]);
      this.telegramPublishBot = telegramPublishBot;
      this.textBuilder = textBuilder;
      this.posts = posts;
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


    internal async Task publishInMainChannel(long postingId)
    {
      if (postingId != 0)
      {
        // Ab in den D-Raum damit
        logger.Info("Es soll folgender Post veröffentlicht werden: " + postingId);
        try
        {
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
          }
          else
          {
            this.posts.resetTextDirtyFlag(postingId);
            this.posts.resetDirtyFlag(postingId);
            this.posts.setChatMsgId(postingId, result.MessageId);
            this.posts.setPublishTimestamp(postingId);
          }
        }
        catch (Exception ex)
        {
          logger.Error(ex, "Fehler beim Veröffentlichen eines Posts im D-Raum, PostingId: " + postingId + " wird neu eingereiht.");
          this.posts.reAcceptFailedPost(postingId);
        }
      }
    }


    internal async Task deletePostFromAllChannels(long postId)
    {
      logger.Info("Es soll folgender Post gelöscht werden (abgelaufen): " + postId);
      long messageId = this.posts.getMessageId(postId);
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
      long messageDailyId = this.posts.getMessageIdDaily(postId);
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
      long messageWeeklyId = this.posts.getMessageIdWeekly(postId);
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

    internal async Task publishSilently(string message)
    {
      try
      {
        await this.telegramPublishBot.SendTextMessageAsync(
          chatId: this.draumChatId,
          disableNotification: true,
          disableWebPagePreview: true,
          text: message).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim stillen Veröffentlichen");
      }
    }

    internal async Task<string> removePostingFromChannels(long postingId)
    {
      int messageId = this.posts.getMessageId(postingId);
      int messageIdDaily = this.posts.getMessageIdDaily(postingId);
      int messageIdWeekly = this.posts.getMessageIdWeekly(postingId);
      string resultText = "Der Beitrag wurde gelöscht";
      if (messageId != -1)
      {
        if (!this.posts.removePost(postingId))
        {
          logger.Error("Konnte den Post "+postingId+" nicht aus dem Datensatz löschen");
          resultText = "Konnte nicht aus dem Datensatz gelöscht werden.";
        }
        try
        {
          // Nachricht aus dem D-Raum löschen
          await this.telegramPublishBot.DeleteMessageAsync(
            chatId: this.draumChatId,
            messageId: messageId);
          if (messageIdDaily != -1)
          {
            await this.telegramPublishBot.DeleteMessageAsync(
              chatId: this.draumDailyChatId,
              messageId: messageIdDaily);
          }
          if (messageIdWeekly != -1)
          {
            await this.telegramPublishBot.DeleteMessageAsync(
              chatId: this.draumWeeklyChatId,
              messageId: messageIdWeekly);
          }
          resultText += "\r\nDer Beitrag wurde aus den Kanälen gelöscht";
        }
        catch (Exception ex)
        {
          logger.Error(ex, "Konnte den Post nicht aus den Kanälen löschen: " + postingId);
          resultText += "\r\nBeim Löschen aus den Chats gab es Probleme";
        }
      }
      else
      {
        logger.Error("Es konnte keine Message-ID gefunden werden (im Chat) um den Beitrag zu löschen : " + postingId);
        resultText = "Der Post "+postingId+" scheint gar nicht veröffentlicht zu sein";
      }
      return resultText;
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