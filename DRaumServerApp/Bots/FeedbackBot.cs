using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class FeedbackBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly TelegramBotClient telegramFeedbackBot;
    private readonly long feedbackChatId;

    private CancellationTokenSource cts = new CancellationTokenSource();

    internal FeedbackBot(TelegramBotClient telegramFeedbackBot,
      Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
      Func<ITelegramBotClient, Exception, CancellationToken, Task> errorHandler)
    {
      this.feedbackChatId = long.Parse(ConfigurationManager.AppSettings["feedbackChatID"]);
      this.telegramFeedbackBot = telegramFeedbackBot;

      var receiverOptions = new ReceiverOptions();
      receiverOptions.AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] { 
        Telegram.Bot.Types.Enums.UpdateType.CallbackQuery,
        Telegram.Bot.Types.Enums.UpdateType.Message  
      };
      receiverOptions.ThrowPendingUpdates = true;
      this.telegramFeedbackBot.StartReceiving(
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
      this.telegramFeedbackBot.StartReceiving(
        updateHandler,
        errorHandler,
        receiverOptions,
        cancellationToken: cts.Token);
    }

    internal async Task removeInlineMarkup(int messageId)
    {
      try
      {
        await this.telegramFeedbackBot.EditMessageReplyMarkupAsync(
          chatId: this.feedbackChatId,
          messageId: messageId,
          replyMarkup: null
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Entfernen des Inline-Keyboards des Feedback-Bots, msg-id: " + messageId);
      }
    }

    internal async Task sendMessage(string message)
    {
      try
      {
        await this.telegramFeedbackBot.SendTextMessageAsync(
          chatId: this.feedbackChatId,
          text: message
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim senden einer Nachricht über den Feedback-Bot, chatid: " + this.feedbackChatId);
      }
    }


    [ItemCanBeNull]
    internal async Task<Message> sendMessageWithKeyboard(string message, InlineKeyboardMarkup keyboard)
    {
      try
      {
        return await this.telegramFeedbackBot.SendTextMessageAsync(
          chatId: this.feedbackChatId,
          text: message,
          replyMarkup: keyboard
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) durch den Feedback-Bot, chatid: " + this.feedbackChatId);
      }
      return null;
    }

  }
}