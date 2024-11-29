namespace AOAIProxy.Models
{
    public class ResponseData
    { 
        public NoStreamChoice[] choices { get; set; }
        public string model { get; set; }
        public Usage usage { get; set; }
    }

   
    public class NoStreamChoice
    {
        public NoStreamContent_Filter_Results content_filter_results { get; set; }
        public string finish_reason { get; set; }
        public int index { get; set; }
        public NoStreamMessage message { get; set; }
    }

    public class NoStreamContent_Filter_Results
    {
        public Hate hate { get; set; }
        public Protected_Material_Code protected_material_code { get; set; }
        public Protected_Material_Text protected_material_text { get; set; }
        public Self_Harm self_harm { get; set; }
        public Sexual sexual { get; set; }
        public Violence violence { get; set; }
    }

    public class Hate
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class Protected_Material_Code
    {
        public bool filtered { get; set; }
        public bool detected { get; set; }
    }

    public class Protected_Material_Text
    {
        public bool filtered { get; set; }
        public bool detected { get; set; }
    }

    public class Self_Harm
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class Sexual
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class Violence
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class NoStreamMessage
    {
        public string content { get; set; }
        public string role { get; set; }
    }

    public class Prompt_Filter_Results
    {
        public int prompt_index { get; set; }
        public Content_Filter_Result content_filter_result { get; set; }
    }

    public class Content_Filter_Result
    {
        public Jailbreak jailbreak { get; set; }
        public Sexual1 sexual { get; set; }
        public Violence1 violence { get; set; }
        public Hate1 hate { get; set; }
        public Self_Harm1 self_harm { get; set; }
    }

    public class Jailbreak
    {
        public bool filtered { get; set; }
        public bool detected { get; set; }
    }

    public class Sexual1
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class Violence1
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class Hate1
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

    public class Self_Harm1
    {
        public bool filtered { get; set; }
        public string severity { get; set; }
    }

}
