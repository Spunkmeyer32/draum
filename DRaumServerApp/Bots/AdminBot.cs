using System;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.TelegramUtilities;
using JetBrains.Annotations;
using NLog;
using NLog.Targets;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class AdminBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly TelegramBotClient telegramAdminBot;

    internal AdminBot(TelegramBotClient telegramAdminBot)
    {
      this.telegramAdminBot = telegramAdminBot;
    }

  
    internal async Task removeMessage(int messageId, long adminChatId)
    {
      try
      {
        await this.telegramAdminBot.DeleteMessageAsync(
          messageId: messageId,
          chatId: adminChatId
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim löschen einer Nachricht an den Admin, Msg-ID: " + messageId);
      }
    }

    internal async Task handleErrorMemory(long chatId, CancellationToken cancelToken)
    {
      MemoryTarget target = LogManager.Configuration.FindTargetByName<MemoryTarget>("errormemory");
      while (target.Logs.Count>0)
      {
        string s = target.Logs[0];
        await this.sendMessageWithKeyboard(chatId, s, Keyboards.getGotItDeleteButtonKeyboard());
        try
        {
          await Task.Delay(3000, cancelToken);
        }
        catch (OperationCanceledException)
        {
          return;
        }
        target.Logs.RemoveAt(0);
        if (cancelToken.IsCancellationRequested)
        {
          return;
        }
      }
    }


    [ItemCanBeNull]
    internal async Task<Message> sendMessage(long chatId, string message)
    {
      try
      {
        return await this.telegramAdminBot.SendTextMessageAsync(
          chatId: chatId,
          text: message
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht an den Admin, chatid: " + chatId);
      }
      return null;
    }

    internal async Task editMessage(long chatId, int messageId, string message)
    {
      try
      {
        await this.telegramAdminBot.EditMessageTextAsync(
          chatId: chatId,
          messageId: messageId,
          text: message).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Aktualisieren einer Nachricht an den Admin, chatid: " + chatId);
      }
    }

    internal async Task replyToCallback(string callbackId, string message)
    {
      try
      {
        await this.telegramAdminBot.AnswerCallbackQueryAsync(
          callbackQueryId: callbackId,
          text: message,
          showAlert: true).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden der Callback-Antwort an den Admin, message: " + message);
      }
    }

    [ItemCanBeNull]
    internal async Task<Message> sendMessageWithKeyboard(long chatId, string message, InlineKeyboardMarkup keyboard)
    {
      try
      {
        return await this.telegramAdminBot.SendTextMessageAsync(
          chatId: chatId,
          text: message,
          replyMarkup: keyboard
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) an den Admin, chatid: " + chatId);
      }
      return null;
    }




  }
}