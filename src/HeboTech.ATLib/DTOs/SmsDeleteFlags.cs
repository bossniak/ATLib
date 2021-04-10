using System.ComponentModel;

namespace HeboTech.ATLib.DTOs
{
    public enum SmsDeleteFlags
    {
        [Description("Delete the message specified in <index>")]
        Del0 = 0,

        [Description("Delete all read messages from preferred message storage, leaving unread messages and stored mobile originated messages(whether sent or not) untouched")]
        Del1 = 1,

        [Description("Delete all read messages from preferred message storage and sent mobile originated messages, leaving unread messages and unsent mobile originated messages untouched")]
        Del2 = 2,

        [Description("Delete all read messages from preferred message storage, sent and unsent mobile originated messages leaving unread messages untouched ")]
        Del3 = 3,

        [Description("Delete all messages from preferred message storage including unread messages")]
        Del4 = 4
    }
}
