namespace GroupAnnouncementApp.Helpers;

// How the resolution time is written into the message body.
public enum TemplateTime
{
    // The message has no blank to fill.
    None,

    // "{time}" becomes minutes, for example 15 (the body already says "minutes").
    Minutes,

    // "{time}" becomes hours and minutes, for example 01:30 (the body already says "hours").
    HoursMinutes
}

public sealed class MessageTemplate
{
    public string Title { get; set; } = string.Empty;

    // The body is fixed. "{time}" marks the blank that the chosen resolution time fills.
    public string Body { get; set; } = string.Empty;

    public TemplateTime TimeKind { get; set; }
}

// The ready-made announcements. Edit titles and bodies here.
public static class MessageTemplates
{
    public static readonly List<MessageTemplate> All = new List<MessageTemplate>
    {
        new MessageTemplate
        {
            Title = "ग्राहक तक्रार दुरुस्ती",
            TimeKind = TemplateTime.Minutes,
            Body = "ग्राहकांना सूचना: घरगुती वीज तक्रार दुरुस्तीसाठी वीजपुरवठा अंदाजे {time} मिनिटे बंद राहील.\nहोणाऱ्या गैरसोयीबद्दल दिलगीर आहोत. कृपया सहकार्य करावे. 🙏"
        },
        new MessageTemplate
        {
            Title = "झाडांच्या फांद्या छाटणे",
            TimeKind = TemplateTime.HoursMinutes,
            Body = "ग्राहकांना सूचना: झाडांच्या फांद्या छाटण्याच्या कामासाठी वीजपुरवठा अंदाजे {time} तास बंद राहील.\nगैरसोयीबद्दल दिलगीर आहोत. कृपया सहकार्य करावे. 🙏"
        },
        new MessageTemplate
        {
            Title = "तातडीचे काम",
            TimeKind = TemplateTime.HoursMinutes,
            Body = "ग्राहकांना सूचना: तातडीच्या विद्युत कामासाठी वीजपुरवठा बंद करण्यात आला आहे. अंदाजे {time} तासांत वीजपुरवठा पूर्ववत होईल. गैरसोयीबद्दल दिलगीर आहोत."
        },
        new MessageTemplate
        {
            Title = "बिघाड पूर्व दुरुस्ती",
            TimeKind = TemplateTime.HoursMinutes,
            Body = "ग्राहकांना सूचना: कोणत्याही मोठा बिघाड होण्यापूर्वी दुरुस्ती करण्याचे काम हाती घेतले आहे सदर काम पूर्ण होण्यास {time} तास वेळ लागेल तरी सहकार्य करावे. धन्यवाद."
        },
        new MessageTemplate
        {
            Title = "लाईन फॉल्ट",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना सूचना: विद्युत वाहिनीत बिघाड झाल्यामुळे दुरुस्तीचे काम सुरू आहे. वीज सुरू होण्याची वेळ अद्याप निश्चित नाही.\nगैरसोयीबद्दल दिलगीर आहोत. पुढील माहिती उपलब्ध झाल्यावर कळवण्यात येईल."
        },
        new MessageTemplate
        {
            Title = "आवाज किंवा स्पार्किंग आढळल्यास",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना विनंती: परिसरात विद्युत तारांमधून आवाज किंवा स्पार्किंग दिसल्यास कृपया त्वरित वायरमनला कळवावे.\nसुरक्षिततेसाठी संबंधित तारांपासून दूर राहावे व स्पर्श करू नये. धन्यवाद!"
        },
        new MessageTemplate
        {
            Title = "बिघाड तपासण्याचे काम सुरू",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना सूचना: वीजपुरवठ्यातील बिघाड शोधण्याचे काम सुरू आहे. वीज सुरू होण्याची वेळ अद्याप निश्चित नाही.\nअधिकृत माहिती उपलब्ध झाल्यावर कळवण्यात येईल. आपल्या सहकार्याबद्दल धन्यवाद!"
        },
        new MessageTemplate
        {
            Title = "लाईन फॉल्ट सापडला",
            TimeKind = TemplateTime.HoursMinutes,
            Body = "ग्राहकांना सूचना: विद्युत वाहिनीतील बिघाड शोधण्यात आला असून दुरुस्तीचे काम सुरू आहे. अंदाजे {time} तासांत वीज सुरू होण्याची अपेक्षा आहे.\nगैरसोयीबद्दल दिलगीर आहोत. कृपया सहकार्य करावे. 🙏"
        },
        new MessageTemplate
        {
            Title = "दुरुस्ती पूर्ण — वीजपुरवठा सुरू",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना सूचना: आपल्या परिसरातील विद्युत बिघाड दुरुस्त करून वीजपुरवठा पूर्ववत करण्यात आला आहे.\nझालेल्या गैरसोयीबद्दल दिलगीर आहोत. सहकार्याबद्दल धन्यवाद!"
        },
        new MessageTemplate
        {
            Title = "लोडशेडिंग",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना सूचना: लोडशेडिंगमुळे काही काळ वीजपुरवठा बंद राहील. लोड मॅनेजमेंट होताच वीजपुरवठा पूर्ववत करण्यात येईल. कृपया सहकार्य करावे."
        },
        new MessageTemplate
        {
            Title = "मेन इनकमिंग सप्लाय बंद",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना सूचना: मुख्य वीजपुरवठा बंद झाल्यामुळे वीज सुरू होण्याची वेळ अद्याप निश्चित नाही. अधिक माहिती मिळताच कळवण्यात येईल. गैरसोयीबद्दल दिलगीर आहोत."
        },
        new MessageTemplate
        {
            Title = "चेंज ओव्हर",
            TimeKind = TemplateTime.None,
            Body = "ग्राहकांना सूचना: मुख्य विद्युत वाहिनीचा पुरवठा बंद झाल्यामुळे पर्यायी व्यवस्था करून विद्युत पुरवठा थोड्याच वेळात चालू करण्यात येत आहे."
        }
    };

    // The last choice in the title list: the admin writes everything.
    public const string CustomTitle = "Custom announcement";

    // The titles for the drop-down: every template, then the custom choice.
    public static List<string> DropDownTitles()
    {
        List<string> titles = new List<string>();
        for (int i = 0; i < All.Count; i++)
        {
            titles.Add(All[i].Title);
        }

        titles.Add(CustomTitle);
        return titles;
    }

    // The body with the resolution time written into the blank (digits are normal 0-9).
    public static string BuildBody(MessageTemplate template, int minutes)
    {
        string time = "__";
        if (minutes > 0)
        {
            if (template.TimeKind == TemplateTime.Minutes)
            {
                time = minutes.ToString();
            }
            else
            {
                time = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
            }
        }

        return template.Body.Replace("{time}", time);
    }
}
