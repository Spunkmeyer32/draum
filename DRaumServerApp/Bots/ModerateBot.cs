﻿using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class ModerateBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly TelegramBotClient telegramModerateBot;

    private readonly long moderateChatId;

    private CancellationTokenSource cts = new CancellationTokenSource();

    internal ModerateBot(TelegramBotClient telegramModerateBot, 
      Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
      Func<ITelegramBotClient, Exception, CancellationToken, Task> errorHandler)
    {
      this.moderateChatId = long.Parse(ConfigurationManager.AppSettings["moderateChatID"]);
      this.telegramModerateBot = telegramModerateBot;

      var receiverOptions = new ReceiverOptions();
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] { 
        Telegram.Bot.Types.Enums.UpdateType.CallbackQuery,
        Telegram.Bot.Types.Enums.UpdateType.Message
      };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramModerateBot.StartReceiving(
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
      this.telegramModerateBot.StartReceiving(
        updateHandler,
        errorHandler,
        receiverOptions,
        cancellationToken: cts.Token);
    }


    internal async Task removeMessage(int messageId)
    {
      try
      {
        await this.telegramModerateBot.DeleteMessageAsync(
          chatId: this.moderateChatId,
          messageId: messageId
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim löschen einer Nachricht an den Moderator, Msg-ID: " + messageId);
      }
    }

    internal async Task<Message> sendMessageWithKeyboard(string message, InlineKeyboardMarkup keyboard, bool sendAsHtml)
    {
      try
      {
        if (sendAsHtml)
        {
          return await this.telegramModerateBot.SendTextMessageAsync(
            chatId: this.moderateChatId,
            text: message,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard
          ).ConfigureAwait(false);
        }
        else
        {
          return await this.telegramModerateBot.SendTextMessageAsync(
            chatId: this.moderateChatId,
            text: message,
            replyMarkup: keyboard
          ).ConfigureAwait(false);
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) an den Moderator, chatid: " + this.moderateChatId);
      }
      return null;
    }

    internal async Task editMessage(int messageId, string message, InlineKeyboardMarkup inlineKeyboardMarkup)
    {
      try
      {
        await this.telegramModerateBot.EditMessageTextAsync(
          chatId: this.moderateChatId,
          messageId: messageId,
          replyMarkup: inlineKeyboardMarkup,
          text: message).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Aktualisieren einer Nachricht an den Admin, chatid: " + this.moderateChatId);
      }
    }

    internal async Task editMessageButtons(int messageId, InlineKeyboardMarkup inlineKeyboardMarkup)
    {
      try
      {
        await this.telegramModerateBot.EditMessageReplyMarkupAsync(
          chatId: this.moderateChatId,
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