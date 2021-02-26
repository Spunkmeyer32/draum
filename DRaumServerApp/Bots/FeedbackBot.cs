using System;
using System.Configuration;
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
    private readonly long feedbackChatId;

    internal FeedbackBot(TelegramBotClient telegramFeedbackBot)
    {
      this.feedbackChatId = long.Parse(ConfigurationManager.AppSettings["feedbackChatID"]);
      this.telegramFeedbackBot = telegramFeedbackBot;
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