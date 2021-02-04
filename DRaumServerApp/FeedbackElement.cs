using System;
using System.Collections.Generic;
using System.Text;

namespace DRaumServerApp
{
  class FeedbackElement
  {
    public String text { get; set; }
    public long chatID { get; set; }

    public FeedbackElement()
    {
      this.text = "";
      this.chatID = -1;
    }

    public FeedbackElement(string text, long id)
    {
      this.text = text;
      this.chatID = id;
    }
  }
}
