using UnityEngine;
using UnityEngine.SceneManagement;

namespace CafeSim
{
    // Business hours for the merged cafe: 09:00 to 18:00, three in-game minutes per real second.
    // Pausing freezes the customer queue, the brew animations and the clock together.
    public sealed class CafeBusinessDay : MonoBehaviour
    {
        public CafeSession session;
        public MapleCafe.BaristaView barista;
        public float openHour=9, closeHour=18, gameSecondsPerSecond=180;
        public int drinkPrice=4500, tipPerStar=500;
        public string mainMenuScene="MainMenu";

        // Survives the scene reload that starts the next day; the main menu starts over at day 1.
        public static int Day {get;private set;}=1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDay()=>Day=1;

        public float Clock {get;private set;}
        public bool Paused {get;private set;}
        public bool Closed {get;private set;}
        public int Served {get;private set;} public int Accurate {get;private set;} public int Missed {get;private set;}
        public int StarTotal {get;private set;} public int Sales {get;private set;} public int Tips {get;private set;}
        public float AverageStars=>Served==0?0:StarTotal/(float)Served;
        public float Progress=>Mathf.InverseLerp(openHour*3600,closeHour*3600,Clock);

        void Awake()
        {
            Clock=openHour*3600;
            AudioListener.volume=CafeSettings.Volume;
            if(barista!=null)barista.MouseSensitivity=CafeSettings.MouseSensitivity;
        }
        void OnEnable(){if(session!=null)session.DialogueOutput+=OnDialogue;}
        void OnDisable(){if(session!=null)session.DialogueOutput-=OnDialogue;}
        void OnDestroy(){Freeze(false);}

        void Update()
        {
            if(Paused||Closed||session==null||session.Queue==null)return;
            Clock=Mathf.Min(closeHour*3600,Clock+Time.unscaledDeltaTime*gameSecondsPerSecond);
            if(Clock>=closeHour*3600){Closed=true;Freeze(true);}
        }
        void OnDialogue(CafeVisit visit,string kind,string line)
        {
            if(Closed)return;
            // Stars are decided when the drink is handed over, so a late review still counts.
            if(kind=="SERVED")
            {
                int stars=visit.Reaction?.stars??0;
                Served++;StarTotal+=stars;if(visit.Accuracy>=.999f)Accurate++;
                Sales+=drinkPrice;Tips+=Mathf.Max(0,stars-3)*tipPerStar;
            }
            else if(kind=="RATING"&&visit.State==VisitState.TimedOut)Missed++;
        }

        public void SetPaused(bool value)
        {
            if(Closed||Paused==value)return;
            Paused=value;Freeze(value);
        }
        void Freeze(bool frozen)
        {
            Time.timeScale=frozen?0:1;AudioListener.pause=frozen;
            if(session!=null)session.Paused=frozen;
        }
        public void NextDay(){Day++;Freeze(false);SceneManager.LoadScene(SceneManager.GetActiveScene().path);}
        public void ExitToMenu(){Day=1;Freeze(false);SceneManager.LoadScene(mainMenuScene);}

        public string ClockText
        {
            get
            {
                // The display ticks in three-minute steps, one per real second.
                int minutes=Mathf.FloorToInt(Clock/180)*3,hour=minutes/60;
                return (hour<12?"오전 ":"오후 ")+hour.ToString("00")+":"+(minutes%60).ToString("00");
            }
        }
    }
}
