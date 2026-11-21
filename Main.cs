using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Serialization;
using BepInEx;
using FluffyUnderware.DevTools.Extensions;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GettingOverItMod
{
    [BepInPlugin("com.shraneid.mymod", "GoI Mod", "1.0.0")]
    public class OmegaSaveMod : BaseUnityPlugin
    {
	    private Boolean setupDone = false;
	    private int frameCount;

	    private Boolean showKeybinds = true;
	    
	    public Narrator narrator;
	    public Rigidbody2D hammer;
	    public SliderJoint2D slider;
	    public HingeJoint2D hinge;
	    public Rigidbody2D cursor;
	    public HingeJoint2D hubJoint;
	    public Transform playerTransform;
	    private JointMotor2D stillHingeMotor;
	    private JointMotor2D stillSliderMotor;
	    private JointMotor2D stillHubMotor;
	    private SaveState debugSave;
	    public PlayerControl pc;
	    public CameraControl cc;
	    private MemoryStream stream;
	    private StreamWriter streamWriter;
	    private XmlSerializer serializer;
	    private string saveString;
	    private byte[] tempByteArray;
	    public Transform spine1;
	    public Transform spine2;
	    
	    private GUIStyle textStyle = new GUIStyle {
		    alignment = TextAnchor.MiddleLeft,
		    fontSize = 40,
		    richText = true
	    };

	    private void Awake()
        {
            Logger.LogInfo("Loaded OmegaSaveMod on startup");
            new Harmony("com.shraneid.mymod").PatchAll();
	    }

	    private void OnGUI()
	    {
		    if (!setupDone) return;
		    if (!showKeybinds) return;
		    
			GUILayout.BeginArea(new Rect(10, 10, 440, 250), GUI.skin.box);
			GUILayout.Label("<b>OmegaSaveMod</b>", textStyle);
			GUILayout.Label("F8  - Omega Save", textStyle);
			GUILayout.Label("F10 - Omega Load", textStyle);
			GUILayout.Label("F1  - Toggle Keybinds", textStyle);
			GUILayout.EndArea();
	    }

	    private void Update()
        {
	        if (!setupDone) return;
	        
	        if (Time.frameCount < frameCount + 80) return;

	        if (Input.GetKey(KeyCode.F1))
	        {
		        frameCount = Time.frameCount;
		        showKeybinds = !showKeybinds;
	        }
	        if (Input.GetKey(KeyCode.F8))
	        {
		        frameCount = Time.frameCount;
		        Debug.Log("Save Game");
		        SaveGameTemp();
		        
		        PopupText popupText = gameObject.AddComponent<PopupText>();
		        popupText.setText("Game Saved");
	        } 
	        else if (Input.GetKey(KeyCode.F10)) 
	        {
		        frameCount = Time.frameCount;
		        Debug.Log("Load Game");
		        LoadTempSave();
		        
		        PopupText popupText = gameObject.AddComponent<PopupText>();
		        popupText.setText("Game Loaded");
	        }
        }

        public void Setup()
        {
	        Saviour saveManager = Object.FindObjectOfType<Saviour>();
		    
	        this.narrator = saveManager.narrator;
	        this.hammer = saveManager.hammer;
	        this.slider = saveManager.slider;
	        this.hinge = saveManager.hinge;
	        this.cursor = saveManager.cursor;
	        this.hubJoint = saveManager.hubJoint;
	        this.playerTransform = saveManager.playerTransform;
	        this.stillHingeMotor = this.hinge.motor;
	        this.stillSliderMotor = this.slider.motor;
	        this.stillHubMotor = this.hubJoint.motor;
	        this.pc = saveManager.pc;
	        this.cc = saveManager.cc;
	        this.stream = new MemoryStream();
	        this.streamWriter = new StreamWriter(this.stream, Encoding.UTF8);
	        this.serializer = new XmlSerializer(typeof(SaveState));
	        this.spine1 = saveManager.spine1;
	        this.spine2 = saveManager.spine2;

	        this.setupDone = true;
        }

        private SaveState Save()
		{
			SaveState saveState = new SaveState();
			saveState.hingeVel = this.hinge.jointSpeed;
			saveState.hingePos = this.hinge.jointAngle;
			saveState.sliderVel = this.slider.jointSpeed;
			saveState.sliderPos = this.slider.jointTranslation;
			saveState.playerPos = this.playerTransform.position;
			saveState.playerRot = this.playerTransform.rotation;
			Rigidbody2D[] componentsInChildren = this.playerTransform.GetComponentsInChildren<Rigidbody2D>();
			saveState.rbLinearVelocities = new Vector2[componentsInChildren.Length];
			saveState.rbAngularVelocities = new float[componentsInChildren.Length];
			saveState.rbPositions = new Vector2[componentsInChildren.Length];
			saveState.rbAngles = new float[componentsInChildren.Length];
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				saveState.rbLinearVelocities[i] = componentsInChildren[i].velocity;
				saveState.rbAngularVelocities[i] = componentsInChildren[i].angularVelocity;
				saveState.rbPositions[i] = componentsInChildren[i].position;
				saveState.rbAngles[i] = componentsInChildren[i].rotation;
			}
			saveState.saveNum = 0;
			saveState.camPos = Camera.main.transform.position;
			this.tempByteArray = this.narrator.getKeyDialogDoneList();
			if (this.tempByteArray != null)
			{
				saveState.keyDialogDone = Convert.ToBase64String(this.tempByteArray);
			}
			else
			{
				saveState.keyDialogDone = Convert.ToBase64String(new byte[this.narrator.KeyDialog.Count]);
			}
			this.tempByteArray = this.narrator.getObservationDialogDoneList();
			if (this.tempByteArray != null)
			{
				saveState.observationDialogDone = Convert.ToBase64String(this.tempByteArray);
			}
			else
			{
				saveState.observationDialogDone = Convert.ToBase64String(new byte[this.narrator.Observations2.Count]);
			}
			this.tempByteArray = this.narrator.getCondolenceDialogDoneList();
			if (this.tempByteArray != null)
			{
				saveState.condolenceDialogDone = Convert.ToBase64String(this.tempByteArray);
			}
			else
			{
				saveState.condolenceDialogDone = Convert.ToBase64String(new byte[this.narrator.Condolences2.Count]);
			}
			saveState.timePlayed = this.narrator.timePlayedThisGame;
			saveState.speedrun = this.narrator.speedrun;
			saveState.version = Application.version;
			return saveState;
		}

        public void SaveGameTemp()
        {
            this.stream = new MemoryStream();
            this.streamWriter = new StreamWriter(this.stream, Encoding.UTF8);
            
            this.debugSave = this.Save();
            this.serializer.Serialize(this.streamWriter, this.debugSave);
            this.saveString = Encoding.UTF8.GetString(this.stream.GetBuffer());
            if (!this.saveString.StartsWith("<?") || !this.saveString.EndsWith(">"))
            {
                this.streamWriter.Flush();
                this.stream.SetLength(0L);
                Debug.LogWarning("malformed save: " + this.saveString);
                return;
            }
            PlayerPrefs.SetString("TempSave", this.saveString);
			PlayerPrefs.Save();
	    }

        public void Load(SaveState loadedSave)
		{
			this.pc.PauseInput(0.5f);
			Rigidbody2D[] componentsInChildren = this.playerTransform.GetComponentsInChildren<Rigidbody2D>();
			if (loadedSave.rbPositions.Length != componentsInChildren.Length)
			{
				return;
			}
			foreach (Rigidbody2D rigidbody2D in componentsInChildren)
			{
			}
			this.spine1.localScale = Vector3.one;
			this.spine2.localScale = Vector3.one;
			Physics2D.simulationMode = SimulationMode2D.Script;
			this.playerTransform.gameObject.SetActive(false);
			this.playerTransform.position = loadedSave.playerPos;
			this.playerTransform.rotation = loadedSave.playerRot;
			this.playerTransform.gameObject.SetActive(true);
			Physics2D.Simulate(Time.fixedDeltaTime * 0.01f);

			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				componentsInChildren[j].position = loadedSave.rbPositions[j];
				componentsInChildren[j].rotation = loadedSave.rbAngles[j];
				componentsInChildren[j].velocity = loadedSave.rbLinearVelocities[j];
				componentsInChildren[j].angularVelocity = loadedSave.rbAngularVelocities[j];
			}
			Physics2D.Simulate(Time.fixedDeltaTime * 0.01f);
			for (int k = 0; k < componentsInChildren.Length; k++)
			{
			}
			this.cc.Teleport(this.playerTransform.position);
			for (int l = 0; l < 50; l++)
			{
				this.cc.FixedUpdate();
			}
			this.narrator.setDialogDoneLists(Convert.FromBase64String(loadedSave.keyDialogDone), Convert.FromBase64String(loadedSave.observationDialogDone), Convert.FromBase64String(loadedSave.condolenceDialogDone));
			this.narrator.speedrun = loadedSave.speedrun;
			Rigidbody2D[] array = componentsInChildren;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].WakeUp();
			}
			this.cursor.position = this.hammer.position;
			this.narrator.timePlayedThisGame = loadedSave.timePlayed;
			this.pc.loadFinished = true;
			this.cc.loadFinished = true;
			this.pc.StartAnimator();
			Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
			this.slider.motor = this.stillSliderMotor;
			this.hinge.motor = this.stillHingeMotor;
			this.hubJoint.motor = this.stillHubMotor;
		}
        
        public bool LoadTempSave()
		{
			this.pc.loadedFromSave = true;
			string saveStateString = PlayerPrefs.GetString("TempSave");
			SaveState saveState = null;
			using (TextReader textReader = new StringReader(saveStateString))
			{
				if (textReader.Peek() != 60)
				{
					textReader.Read();
				}
				try
				{
					saveState = (SaveState)this.serializer.Deserialize(textReader);
				}
				catch (InvalidOperationException ex)
				{
					Debug.LogWarning("failed to load save position 1");
					Debug.LogWarning(ex.Message);
					Debug.LogWarning(ex.StackTrace);
					Debug.LogWarning(textReader);
				}
			}
			Debug.Log("Save loaded at time: " + DateTime.UtcNow.ToString());
			Load(saveState);
			return true;
		}
    }

	[HarmonyPatch(typeof(Saviour), "Start")]
    public static class Saviour_Start_Patch
    {
        public static void Postfix()
        {
	        Object.FindObjectOfType<OmegaSaveMod>().Setup();
        }
    }

	public class PopupText : MonoBehaviour
	{
		private const int framesToLive = 200;
		private int currentFrame = 0;
		private string text;

		private GUIStyle style;
		
		private void Awake()
		{
			style = new GUIStyle
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 50,
				richText = true
			};
			style.normal.textColor = Color.red;
		}

		public void setText(string textToShow)
		{
			text = textToShow;
		}

		private void Update()
		{
			currentFrame++;
			
			if (currentFrame >= framesToLive) Destroy(this);
		}

		private void OnGUI()
		{
			if (text == null) return;
			
			GUI.Label(
				new Rect(0, (int)Math.Round(Screen.height / 2.0f - 30 - currentFrame/3.0f), Screen.width, 60), 
				text, 
				style
			);
		}
	}
}