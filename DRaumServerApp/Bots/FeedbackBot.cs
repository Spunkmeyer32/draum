using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DRaumServerApp.Bots
{
  internal class FeedbackBot
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly TelegramBotClient telegramFeedbackBot;

    internal FeedbackBot(TelegramBotClient telegramFeedbackBot)
    {
      this.telegramFeedbackBot = telegramFeedbackBot;
    }
    
    internal async Task removeInlineMarkup(long chatId, int messageId)
    {
      try
      {
        await this.telegramFeedbackBot.EditMessageReplyMarkupAsync(
          chatId: chatId,
          messageId: messageId,
          replyMarkup: null
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Entfernen des Inline-Keyboards des Feedback-Bots, msg-id: " + messageId);
      }
    }

    internal async Task sendMessage(long chatId, string message)
    {
      try
      {
        await this.telegramFeedbackBot.SendTextMessageAsync(
          chatId: chatId,
          text: message
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim senden einer Nachricht über den Feedback-Bot, chatid: " + chatId);
      }
    }


    [ItemCanBeNull]
    internal async Task<Message> sendMessageWithKeyboard(long chatId, string message, InlineKeyboardMarkup keyboard)
    {
      try
      {
        return await this.telegramFeedbackBot.SendTextMessageAsync(
          chatId: chatId,
          text: message,
          replyMarkup: keyboard
        ).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Fehler beim Senden einer Nachricht (Mit Inline-Keyboard) durch den Feedback-Bot, chatid: " + chatId);
      }
      return null;
    }

  }
}