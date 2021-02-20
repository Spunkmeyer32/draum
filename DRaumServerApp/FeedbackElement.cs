using System;
using System.Collections.Generic;
using System.Text;

namespace DRaumServerApp
{
  class FeedbackElement
  {
    public String Text { get; set; }
    public long ChatId { get; set; }

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
