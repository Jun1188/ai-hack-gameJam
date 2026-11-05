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
        public int drinkPrice=4500, tipPerStar=500, wastedCupCost=1600;
        // Per-drink menu prices; a drink missing here sells for drinkPrice.
        public CafeDrinkPrice[] prices={
            new CafeDrinkPrice{menuId="menu_americano",price=4500},new CafeDrinkPrice{menuId="menu_espresso",price=4000},
            new CafeDrinkPrice{menuId="menu_latte",price=5000},new CafeDrinkPrice{menuId="menu_vanilla",price=5500},
            new CafeDrinkPrice{menuId="menu_condensed",price=5500},new CafeDrinkPrice{menuId="menu_choco",price=5500},
            new CafeDrinkPrice{menuId="menu_matcha",price=5500},new CafeDrinkPrice{menuId="menu_strawberry",price=6000},
            new CafeDrinkPrice{menuId="menu_yuja",price=5000},new CafeDrinkPrice{menuId="menu_lemonade",price=5500},
            new CafeDrinkPrice{menuId="menu_ssanghwa",price=5000}};
        public readonly CafeDayLedger Ledger=new CafeDayLedger();
        public string mainMenuScene="MainMenu";

        // Survives the scene reload that starts the next day; the main menu starts over at day 1.
        public static int Day {get;private set;}=1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDay()=>Day=1;
        // After the ending, "게임 시작" continues as free play from the ending's morning.
        public static void ContinueFrom(int day)=>Day=Mathf.Max(1,day);

        public float Clock {get;private set;}
        public bool Paused {get;private set;}
        public bool Closed {get;private set;}
        public int Served {get;private set;} public int Accurate {get;private set;} public int Missed {get;private set;}
        public int StarTotal {get;private set;}
        public int Sales=>Ledger.Sales; public int Tips=>Ledger.Tips;
        public float AverageStars=>Served==0?0:StarTotal/(float)Served;
        public float Progress=>Mathf.InverseLerp(openHour*3600,closeHour*3600,Clock);

        void Awake()
        {
            Clock=openHour*3600;
            AudioListener.volume=CafeSettings.Volume;
            if(barista!=null)barista.MouseSensitivity=CafeSettings.MouseSensitivity;
            var collection=CafeCollection.Instance;collection.BeginDay();
            if(session!=null&&session.personaData!=null&&session.menuData!=null)
                collection.Bind(JsonUtility.FromJson<PersonaTable>(session.personaData.text),JsonUtility.FromJson<MenuTable>(session.menuData.text));
        }
        void OnEnable(){if(session!=null)session.DialogueOutput+=OnDialogue;}
        void OnDisable(){if(session!=null)session.DialogueOutput-=OnDialogue;}
        void OnDestroy(){Freeze(false);}

        void Update()
        {
            if(Paused||Closed||session==null||session.Queue==null)return;
            Clock=Mathf.Min(closeHour*3600,Clock+Time.unscaledDeltaTime*gameSecondsPerSecond);
            if(Clock>=closeHour*3600){Closed=true;Freeze(true);CafeCollection.Instance.EndDay(Day,Ledger);}
        }
        void OnDialogue(CafeVisit visit,string kind,string line)
        {
            if(Closed)return;
            CafeCollection.Instance.Observe(visit,kind,line,session!=null?session.Queue?.Knowledge:null,Day);
            // Stars are decided when the drink is handed over, so a late review still counts.
            if(kind=="USER")Ledger.Ask(visit);
            else if(kind=="SERVED")
            {
                int stars=visit.Reaction?.stars??0;
                Served++;StarTotal+=stars;if(visit.Accuracy>=.999f)Accurate++;
                // The customer pays for what they ordered (the menu item behind their request).
                Ledger.Served(visit,PriceOf(visit.Order?.menuId),tipPerStar,wastedCupCost);
            }
            else if(kind=="RATING"&&visit.State==VisitState.TimedOut){Missed++;Ledger.TimedOut(visit);}
        }
        public int PriceOf(string menuId)
        {
            if(prices!=null)foreach(var p in prices)if(p!=null&&p.menuId==menuId)return p.price;
            return drinkPrice;
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
