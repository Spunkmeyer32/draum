using System;
using System.Threading.Tasks;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class ModerateBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly TelegramBotClient telegramModerateBot;

    internal ModerateBot(TelegramBotClient telegramModerateBot)
    {
      this.telegramModerateBot = telegramModerateBot;
    }


    internal async Task removeMessage(long chatid, int messageId)
    {
      try
      {
        await this.telegramModerateBot.DeleteMessageAsync(
          chatId: chatid,
          messageId: messageId
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim löschen einer Nachricht an den Moderator, Msg-ID: " + messageId);
      }
    }

    internal async Task<Message> sendMessage(long chatId, string message)
    {
      try
      {
        return await this.telegramModerateBot.SendTextMessageAsync(
          chatId: chatId,
          text: message
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht an den Moderator, chatid: " + chatId);
      }
      return null;
    }

    internal async Task<Message> sendMessageWithKeyboard(long chatId, string message, InlineKeyboardMarkup keyboard, bool sendAsHtml)
    {
      try
      {
        if (sendAsHtml)
        {
          return await this.telegramModerateBot.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard
          ).ConfigureAwait(false);
        }
        else
        {
          return await this.telegramModerateBot.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            replyMarkup: keyboard
          ).ConfigureAwait(false);
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) an den Moderator, chatid: " + chatId);
      }
      return null;
    }

    internal async Task editMessage(long chatId, int messageId, string message, InlineKeyboardMarkup inlineKeyboardMarkup)
    {
      try
      {
        await this.telegramModerateBot.EditMessageTextAsync(
          chatId: chatId,
          messageId: messageId,
          replyMarkup: inlineKeyboardMarkup,
          text: message).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Aktualisieren einer Nachricht an den Admin, chatid: " + chatId);
      }
    }

    internal async Task editMessageButtons(long chatId, int messageId, InlineKeyboardMarkup inlineKeyboardMarkup)
    {
      try
      {
        await this.telegramModerateBot.EditMessageReplyMarkupAsync(
          chatId: chatId,
          messageId: messageId,
          replyMarkup: inlineKeyboardMarkup
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Aktualisieren der Reply-Buttons der Message " +  messageId + " des Moderators");
      }
    }

    internal async Task replyToCallback(string callbackId, string message)
    {
      try
      {
        await this.telegramModerateBot.AnswerCallbackQueryAsync(
          callbackQueryId: callbackId,
          text: message,
          showAlert: true).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden der Callback-Antwort an den Moderator, message: " + message);
      }
    }


  }
}