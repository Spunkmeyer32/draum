

namespace DRaumServerApp
{
  class FeedbackElement
  {
    public string Text { get; }
    public long ChatId { get; }

    public FeedbackElement()
    {
      this.Text = "";
      this.ChatId = -1;
    }

    public FeedbackElement(string text, long id)
    {
      this.Text = text;
      this.ChatId = id;
    }
  }
}
