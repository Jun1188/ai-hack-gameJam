using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CafeSim
{
    // Generate -> filter -> rank. This is a bounded rule-based ranker, not a trained
    // Korean fluency/entailment model. Only authored phrases and DB topic slots are realized.
    public static class CafeReplyComposer
    {
        enum Intent { Greeting, Acknowledgement, Question, Unclear }
        sealed class Candidate { public string Speech; public Intent Intent; public float Relevance; }
        public static string Compose(CafeVisit visit,string input,ReplyVocabulary words,Func<string,string> expand)
        {
            string normalized=CafeConversation.Normalize(input);
            Intent intent=Regex.IsMatch(normalized,@"^(안녕|안녕하세요|반갑습니다|반가워요|좋은아침|좋은하루)$")?Intent.Greeting:
                Regex.IsMatch(normalized,@"^(네|예|응|아하|그렇군요|그랬군요|맞아요|알겠어요|좋네요|감사합니다|고마워요)$")?Intent.Acknowledgement:
                Regex.IsMatch(normalized,"왜|어떻|어땠|무슨|무엇|어떤|궁금|언제|어디|얼마|누구|알려|들려")||input.Contains("?")?Intent.Question:Intent.Unclear;
            var candidates=new List<Candidate>();
            void Add(string text,Intent kind,float relevance)
            {
                text=Regex.Replace(text??"",@"\s+"," ").Trim();
                if(!WellFormed(text)||candidates.Exists(c=>c.Speech==text))return;
                candidates.Add(new Candidate{Speech=text,Intent=kind,Relevance=relevance});
            }
            if(intent==Intent.Greeting)
                foreach(var greeting in words.greetings??new[]{"안녕하세요.","반가워요."})Add(greeting,Intent.Greeting,1);
            else if(intent==Intent.Acknowledgement)
                foreach(var acknowledgement in words.acknowledgements)Add(acknowledgement,Intent.Acknowledgement,1);
            else
            {
                // General clarification is always eligible. Do not fabricate an answer
                // by turning the employee's words into a customer fact.
                foreach(var question in words.clarifications)
                {
                    Add(question,intent,.35f);
                    foreach(var acknowledgement in words.acknowledgements)Add(acknowledgement+" "+question,intent,.35f);
                }
                foreach(var raw in (visit.Person.smalltalkTopics??"").Split(','))
                {
                    string topic=raw.Trim();
                    if(topic.Length<2||topic.Length>40||string.IsNullOrEmpty(words.topicQuestion))continue;
                    float relevance=normalized.Contains(CafeConversation.Normalize(topic))?1:
                        CafeConversation.Similarity(expand(input),expand(topic));
                    // Require topic evidence in the current input, not merely in the
                    // persona. Unrelated words must not trigger an arbitrary subject.
                    if(relevance<.2f)continue;
                    string question=words.topicQuestion.Replace("{topic}",topic);
                    Add(question,intent,.65f+relevance);
                    foreach(var acknowledgement in words.acknowledgements)Add(acknowledgement+" "+question,intent,.65f+relevance);
                }
            }
            string best=null;float bestScore=float.NegativeInfinity;
            foreach(var candidate in candidates)
            {
                float score=3*candidate.Relevance+(candidate.Intent==intent?2:0);
                // Prefer one or two complete authored sentences; avoid unnecessary filler.
                score-=candidate.Speech.Length*.002f;
                int compared=0;
                for(int i=visit.History.Count-1;i>=0&&compared<3;i--)
                {
                    if(!visit.History[i].StartsWith("REPLY\n",StringComparison.Ordinal))continue;
                    string previous=visit.History[i].Substring(6);compared++;
                    float similarity=CafeConversation.Similarity(candidate.Speech,previous);
                    score-=CafeConversation.Normalize(candidate.Speech)==CafeConversation.Normalize(previous)?8:similarity*1.5f;
                }
                if(score>bestScore){bestScore=score;best=candidate.Speech;}
            }
            return best??"어떤 뜻인지 조금 더 이야기해 주시겠어요?";
        }
        public static bool WellFormed(string text)
        {
            if(!CafePrompts.ValidSpeech(text)||!Regex.IsMatch(text??"",@"[가-힣]")||
                Regex.IsMatch(text,@"[{}\[\]]|[.!?]{2,}|[\r\n]"))return false;
            // This checks surface constraints, not general Korean grammaticality.
            var sentences=Regex.Split(text,@"[.!?]+");var seen=new HashSet<string>();int count=0;
            foreach(var sentence in sentences)
            {
                string key=CafeConversation.Normalize(sentence);if(key.Length==0)continue;
                if(!seen.Add(key)||++count>2)return false;
            }
            return count>0&&Regex.IsMatch(text,@"[.!?]$");
        }
    }
}
