using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp.TelegramUtilities;
using JetBrains.Annotations;
using NLog;
using NLog.Targets;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class AdminBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

   

    private readonly TelegramBotClient telegramAdminBot;
    private readonly long adminChatId;
    private string lastSentMessage;

    private readonly object lastSentMessageMutex = new object();

    private CancellationTokenSource cts = new CancellationTokenSource();

    internal AdminBot(TelegramBotClient telegramAdminBot, 
      Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
      Func<ITelegramBotClient, Exception, CancellationToken, Task> errorHandler)
    {
      this.lastSentMessage = "";
      this.adminChatId = long.Parse(ConfigurationManager.AppSettings["adminChatID"]);
      this.telegramAdminBot = telegramAdminBot;


      var receiverOptions = new ReceiverOptions();
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[]{ Telegram.Bot.Types.Enums.UpdateType.CallbackQuery };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramAdminBot.StartReceiving(
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
      this.telegramAdminBot.StartReceiving(
        updateHandler,
        errorHandler,
        receiverOptions,
        cancellationToken: cts.Token);
    }

    internal async Task removeMessage(int messageId)
    {
      try
      {
        await this.telegramAdminBot.SendTextMessageAsync(
          chatId: adminChatId,
          text: "Ein Posting soll gelöscht werden.",
          replyMarkup: Keyboards.getPostLinkWithCustomText(messageId, DRaumManager.Roomname, "Springe zum Post")
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Lösch-Nachricht an den Admin, Msg-ID: " + messageId);
      }
    }

    private bool needToSendThis(string message)
    {
      lock(lastSentMessageMutex)
      {
        return !lastSentMessage.Equals(message,StringComparison.InvariantCultureIgnoreCase);
      }
    }

    private void updateLastSentMessage(string message)
    {
      lock(lastSentMessageMutex)
      {
        lastSentMessage = message;
      }
    }

    internal async Task handleErrorMemory(CancellationToken cancelToken)
    {
      MemoryTarget target = LogManager.Configuration.FindTargetByName<MemoryTarget>("errormemory");
      while (target.Logs.Count>0)
      {
        string s = target.Logs[0];       
        if(needToSendThis(s))
        {
          await this.sendMessageWithKeyboard(s, Keyboards.getGotItDeleteButtonKeyboard());
          try
          {
            await Task.Delay(3000, cancelToken);
          }
          catch (OperationCanceledException)
          {
            return;
          }
          updateLastSentMessage(s);
        }
        target.Logs.RemoveAt(0);
        if (cancelToken.IsCancellationRequested)
        {
          return;
        }
      }
    }


    [ItemCanBeNull]
    internal async Task<Message> sendMessage(string message)
    {
      try
      {
        return await this.telegramAdminBot.SendTextMessageAsync(
          chatId: adminChatId,
          text: message
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht an den Admin, chatid: " + adminChatId);
      }
      return null;
    }

    internal async Task editMessage(int messageId, string message)
    {
      try
      {
        await this.telegramAdminBot.EditMessageTextAsync(
          chatId: adminChatId,
          messageId: messageId,
          text: message).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Aktualisieren einer Nachricht an den Admin, chatid: " + adminChatId);
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
    internal async Task<Message> sendMessageWithKeyboard(string message, InlineKeyboardMarkup keyboard)
    {
      try
      {
        return await this.telegramAdminBot.SendTextMessageAsync(
          chatId: adminChatId,
          text: message,
          replyMarkup: keyboard
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) an den Admin, chatid: " + adminChatId);
      }
      return null;
    }


  }
}