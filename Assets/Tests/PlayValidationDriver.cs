#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace NailBalance.Editor
{
    [InitializeOnLoad]
    public static class PlayValidation
    {
        static PlayValidation()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating
                    && SessionState.GetBool("NailBalance.RunValidation", false)
                    && EditorApplication.timeSinceStartup > SessionState.GetFloat("NailBalance.ValidationAfter", float.MaxValue)
                    && !UnityEngine.Object.FindAnyObjectByType<PlayValidationDriver>())
                    new GameObject("EditorOnlyPlayValidation").AddComponent<PlayValidationDriver>();
            };
        }

        [MenuItem("Nail Balance/Validate full game in Play Mode")]
        public static void Run()
        {
            SessionState.SetBool("NailBalance.SmokeOnly", false);
            SessionState.SetBool("NailBalance.RunValidation", true);
            SessionState.SetFloat("NailBalance.ValidationAfter", (float)EditorApplication.timeSinceStartup + 8);
            SessionState.SetInt("NailBalance.OldTutorialPreference", PlayerPrefs.GetInt("NailBalance.TutorialSeen", 0));
            for(int i=0;i<8;i++) {
                SessionState.SetInt("NailBalance.OldScore."+i,PlayerPrefs.GetInt("NailBalance.Score."+i,-1));
                PlayerPrefs.DeleteKey("NailBalance.Score."+i);
            }
            PlayerPrefs.Save();
            PlayerPrefs.SetInt("NailBalance.TutorialSeen", 0);
            EditorSceneManager.OpenScene("Assets/Scenes/NailBalance.unity");
            SetGameSize(540, 960);
            EditorApplication.EnterPlaymode();
        }

        public static void RunSmoke()
        {
            Run();
            SessionState.SetBool("NailBalance.SmokeOnly", true);
        }

        public static void SetGameSize(int width, int height)
        {
            Assembly assembly = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
            Type kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, new object[] { Enum.ToObject(kindType, 1), width, height, "Nail Balance QA " + width + "x" + height });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            sizesType.GetMethod("SaveToHDD").Invoke(sizes, null);
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            Type gameViewType = assembly.GetType("UnityEditor.GameView");
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, count - 1);
            window.Focus();
        }
    }

    // Only compiled into the editor. All tests use normal UI and pointer input; no victory bypasses.
    public class PlayValidationDriver : MonoBehaviour
    {
        [Serializable] class Solution { public float[] x, angle; }
        [Serializable] class Solutions { public List<Solution> solutions; }
        GameController game;
        Mouse mouse;
        Touchscreen touch;
        readonly List<string> evidence = new List<string>();
        readonly List<string> errors = new List<string>();
        Vector2 pointer;

        void Start()
        {
            Application.logMessageReceived += Log;
            InputSystem.settings = ScriptableObject.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (InputDevice device in InputSystem.devices)
                if (device is Mouse) InputSystem.DisableDevice(device);
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            StartCoroutine(Guarded(Scenario()));
        }
        void Log(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition);
        }

        IEnumerator Scenario()
        {
            yield return null; yield return null;
            game = FindAnyObjectByType<GameController>();
            Check(game != null && game.State == GameController.Phase.Menu, "Main menu opens");
            Check(game.levels.Length == 2 && game.levels[0].tasks.Length == 4 && game.levels[1].tasks.Length == 4, "Exactly two levels, four tasks each");
            yield return Capture("menu-540x960");
            yield return ClickButton("MainMenu","SettingsButton");
            Check(game.State==GameController.Phase.Menu,"Settings placeholder does nothing");
            yield return ClickButton("MainMenu","LevelsButton");
            Check(game.State==GameController.Phase.LevelSelect && game.ui.taskButtons.Length==8,"List shows two levels and eight tasks");
            for(int j=0;j<8;j++) Check(game.ui.taskButtons[j].interactable==(j==0),"Initial unlock state "+j);
            game.SelectTask(7);
            Check(game.State==GameController.Phase.LevelSelect,"Locked task cannot be started");
            yield return Capture("level-list-locked");
            yield return ClickButton("LevelSelectScreen","BackButton");
            yield return ClickButton("MainMenu", "PlayButton");
            Check(game.State == GameController.Phase.Tutorial, "First launch shows tutorial (actual: " + game.State + ")");
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Capture("tutorial-540x960");
            yield return ClickButton("TutorialScreen", "UnderstoodButton");
            Check(game.State == GameController.Phase.Playing, "Tutorial dismisses into game");
            Check(game.HeldNail.GetComponents<Collider2D>().Length == 3, "Nail has shaft, head and tip colliders");
            yield return Capture("game-540x960");

            Vector2 nailScreen = NailScreen();
            yield return Click(nailScreen);
            Check(Mathf.Abs(Mathf.DeltaAngle(game.HeldNail.transform.eulerAngles.z, 90)) < 0.5f, "Mouse tap turns nail 90 degrees");
            yield return Click(NailScreen()); yield return Click(NailScreen()); yield return Click(NailScreen());
            yield return Drag(NailScreen(), WorldScreen(new Vector2(1, 2.35f)));
            Check(Mathf.Abs(game.HeldNail.transform.position.x - 1) < 0.08f, "Mouse drag moves nail horizontally");
            Vector2 fastFrom = NailScreen(), fastTo = WorldScreen(new Vector2(1.4f, 2.35f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = fastFrom }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = fastTo }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = fastTo });
            yield return null; yield return null;
            Check(Mathf.Abs(game.HeldNail.transform.position.x - 1.4f) < 0.08f && Mathf.Abs(Mathf.DeltaAngle(0, game.HeldNail.transform.eulerAngles.z)) < 0.5f, "Fast drag within one frame moves instead of rotating: x=" + game.HeldNail.transform.position.x + ", angle=" + game.HeldNail.transform.eulerAngles.z);
            yield return Drag(NailScreen(), WorldScreen(new Vector2(9, 2.35f)));
            Check(game.HeldNail.transform.position.x <= game.horizontalLimit, "Horizontal movement is bounded");
            yield return MoveTo(0);
            RectTransform slider = (RectTransform)game.ui.angleSlider.transform;
            Vector2 sliderCenter = RectTransformUtility.WorldToScreenPoint(null, slider.position);
            yield return Drag(sliderCenter, sliderCenter + new Vector2(45, 0));
            Check(Mathf.Abs(Mathf.DeltaAngle(0, game.HeldNail.transform.eulerAngles.z)) > 10, "Fine rotation slider responds to pointer drag");
            game.ui.angleSlider.value = 0;

            yield return ClickButton("GameHUD", "PauseButton");
            Check(game.State == GameController.Phase.Paused, "Pause button opens pause screen");
            float time = game.RemainingTime;
            yield return new WaitForSecondsRealtime(0.3f);
            Check(game.RemainingTime == time && Time.timeScale == 0, "Pause freezes timer and simulation");
            yield return ClickButton("PauseScreen", "ResumeButton");
            yield return new WaitForSeconds(0.15f);
            Check(game.RemainingTime < time, "Resume restarts timer");

            if (SessionState.GetBool("NailBalance.SmokeOnly", false))
            {
                RectTransform handle = game.ui.angleSlider.handleRect;
                Check(Mathf.Abs(handle.rect.width - handle.rect.height) < 1, "Slider handle is square and does not overlap its label");
                PlayValidation.SetGameSize(720, 1600);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture("game-720x1600");
                Vector3[] corners = new Vector3[4];
                foreach (Button button in game.ui.hud.GetComponentsInChildren<Button>())
                {
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    Check(corners[0].x >= 15 && corners[2].x <= Screen.width - 15 && corners[0].y >= 0 && corners[2].y <= Screen.height, "Portrait button fits with margins: " + button.name);
                }
                Check(errors.Count == 0, "No errors during UI smoke test");
                Finish(true, "UI smoke test passed.");
                yield break;
            }

            // Exercise timeout without changing the puzzle's actual time limit.
            Time.timeScale = 20;
            yield return Until(() => game.State == GameController.Phase.Failure, 5, "Timeout failure");
            Check(game.Score == 0 && game.ui.failureReason.text.Contains("Время"), "Timeout does not award points");
            yield return ClickButton("FailureScreen", "RetryButton");
            Check(game.LevelIndex == 0 && game.TaskIndex == 0 && game.DroppedCount == 0 && game.Score == 0, "Retry resets only current task");

            Solutions solutions = JsonUtility.FromJson<Solutions>(File.ReadAllText("Logs/physics-solutions.json"));
            int lastScore = 0;
            Color firstTheme = game.gameCamera.backgroundColor;
            for (int i = 0; i < 8; i++)
            {
                Check(game.LevelIndex == i / 4 && game.TaskIndex == i % 4, "Task reached " + (i / 4 + 1) + "." + (i % 4 + 1));
                if (i == 1)
                {
                    game.ui.angleSlider.value = 90;
                    yield return MoveTo(3);
                    yield return SwipeDrop(false);
                    yield return Until(() => game.State == GameController.Phase.Failure, 6, "Fallen nail failure");
                    Check(game.Score == lastScore, "Fall failure preserves earned score without extra reward");
                    yield return Capture("failure-540x960");
                    yield return ClickButton("FailureScreen", "RetryButton");
                    Check(game.TaskIndex == 1 && game.DroppedCount == 0, "Fall retry stays on current task");
                }
                if (i == 4) {
                    Check(game.gameCamera.backgroundColor != firstTheme, "Level two has different palette");
                    Check(game.Support.name.StartsWith("YSupport"),"Second level begins with Y support");
                    yield return Capture("Y-support");
                }
                Solution solution = solutions.solutions[i];
                for (int n = 0; n < solution.x.Length; n++)
                {
                    yield return Until(() => game.CanControl, 3, "Next held nail");
                    game.ui.angleSlider.value = Mathf.DeltaAngle(0, solution.angle[n]);
                    yield return MoveTo(solution.x[n]);
                    int before = game.DroppedCount;
                    yield return SwipeDrop(i == 0 && n == 0);
                    Check(game.DroppedCount == before + 1, (i == 0 && n == 0 ? "Touch" : "Mouse") + " downward swipe releases a nail");
                    NailPiece[] pieces = FindObjectsByType<NailPiece>();
                    foreach (NailPiece piece in pieces)
                        if (piece.Released) Check(piece.Body.simulated && piece.Body.bodyType == RigidbodyType2D.Dynamic && piece.Body.constraints == RigidbodyConstraints2D.None, "Released nails remain dynamic");
                    if (n < solution.x.Length - 1) yield return new WaitForSeconds(1.85f);
                }
                float lastDrop = Time.realtimeSinceStartup;
                Check(game.State == GameController.Phase.Playing && game.StableTime < 3, "Last release does not instantly win");
                yield return Until(() => game.State != GameController.Phase.Playing, 16, "Physical stability result");
                Check(game.State == GameController.Phase.Success, "Real physical success for task " + (i + 1));
                Check(Time.realtimeSinceStartup - lastDrop >= 2.9f && game.StableTime >= 3, "Three continuous stability seconds before success");
                Check(game.Score - lastScore == 100 + Mathf.FloorToInt(game.RemainingTime) * 2, "Exact base score and remaining-time bonus");
                lastScore = game.Score;
                if (i == 0) yield return Capture("success-540x960");
                yield return new WaitForSecondsRealtime(2.1f);
                Check(game.State==GameController.Phase.Success,"Victory waits for Next button");
                Check(game.TaskScores[i]>0 && PlayerPrefs.GetInt("NailBalance.Score."+i)==game.TaskScores[i],"Task score persisted");
                if(i==4) yield return Capture("Y-victory");
                yield return ClickButton("SuccessScreen","NextTaskButton");
                if (i == 3)
                {
                    Check(game.State == GameController.Phase.LevelComplete, "Four tasks lead to level-complete screen");
                    yield return Capture("level-complete-540x960");
                    yield return ClickButton("LevelCompleteScreen", "NextLevelButton");
                }
                if (i == 4) yield return Capture("level-two-540x960");
            }
            Check(game.State == GameController.Phase.Complete, "All eight tasks lead to final screen");
            yield return Capture("complete-540x960");
            yield return ClickButton("GameCompleteScreen", "RestartButton");
            Check(game.State==GameController.Phase.LevelSelect,"Final button returns to task list");
            for(int j=0;j<8;j++) Check(game.ui.taskButtons[j].interactable && game.TaskScores[j]>0,"Completed task unlocked "+j);
            yield return Capture("level-list-complete");
            yield return ClickButton("LevelSelectScreen","TaskButton0");
            for(int n=0;n<3;n++) { yield return Until(()=>game.CanControl,3,"Replay nail"); game.ui.angleSlider.value=0; yield return SwipeDrop(false); yield return new WaitForSeconds(1.85f); }
            yield return Until(()=>game.State!=GameController.Phase.Playing,16,"Replay result");
            Check(game.State==GameController.Phase.Success && game.Score==lastScore && game.ui.successScore.text.Contains("+0"),"Replay awards no additional score");
            yield return Capture("replay-victory");
            yield return ClickButton("SuccessScreen","HomeButton");
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.LoadScene("NailBalance");
            yield return null; yield return null;
            game=FindAnyObjectByType<GameController>();
            Check(game.State==GameController.Phase.Menu && game.Score==lastScore && game.NextTask==8,"Scores and progress survive scene restart");
            Check(game.ui.menuScore.text.Contains(lastScore.ToString()),"Main menu displays sum of saved scores");
            yield return ClickButton("MainMenu","PlayButton");
            yield return ClickButton("GameHUD", "HomeButton");
            Check(game.State == GameController.Phase.Menu, "Home returns to menu");
            yield return ClickButton("MainMenu", "HelpButton");
            yield return ClickButton("TutorialScreen", "UnderstoodButton");
            Check(game.State == GameController.Phase.Menu, "Help opened from menu returns to menu");
            PlayValidation.SetGameSize(720, 1600);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return ClickButton("MainMenu", "PlayButton");
            yield return Capture("game-720x1600");
            Check(Screen.width == 720 && Screen.height == 1600, "Tall portrait resolution applied");
            Check(errors.Count == 0, "No Unity errors or exceptions during full playthrough");
            Finish(true, "All checks passed.");
        }

        IEnumerator ClickButton(string screen, string name)
        {
            GameObject root = GameObject.Find(screen);
            if (!root) throw new Exception("Screen not active: " + screen);
            Button target = Array.Find(root.GetComponentsInChildren<Button>(), b => b.name == name);
            if (!target) throw new Exception("Button not found: " + screen + "/" + name);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, target.transform.position));
        }
        IEnumerator Click(Vector2 point)
        {
            MouseState state = new MouseState { position = point };
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        }
        IEnumerator Drag(Vector2 from, Vector2 to)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(MouseButton.Left)); yield return null; yield return null;
            for (int i = 1; i <= 6; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(from, to, i / 6f) }.WithButton(MouseButton.Left)); yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = to }); yield return null; yield return null;
        }
        IEnumerator MoveTo(float x)
        {
            if (Mathf.Abs(game.HeldNail.transform.position.x - x) < 0.001f) yield break;
            yield return Drag(NailScreen(), WorldScreen(new Vector2(x, 2.35f)));
        }
        IEnumerator SwipeDrop(bool useTouch)
        {
            Vector2 from = NailScreen(), to = from + Vector2.down * Screen.height * 0.1f;
            if (!useTouch) { yield return Drag(from, to); yield break; }
            touch = InputSystem.AddDevice<Touchscreen>(); touch.MakeCurrent();
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = from, phase = UnityEngine.InputSystem.TouchPhase.Began });
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = to, phase = UnityEngine.InputSystem.TouchPhase.Moved });
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = to, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
            InputSystem.RemoveDevice(touch); touch = null; mouse.MakeCurrent();
        }
        Vector2 NailScreen() => game.gameCamera.WorldToScreenPoint(game.HeldNail.transform.position);
        Vector2 WorldScreen(Vector2 position) => game.gameCamera.WorldToScreenPoint(position);
        IEnumerator Until(Func<bool> condition, float timeout, string reason)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new Exception("Timed out: " + reason + ", phase=" + game.State);
                yield return null;
            }
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory("Logs/QA");
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes("Logs/QA/" + name + ".png", texture.EncodeToPNG()); Destroy(texture);
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            evidence.Add("PASS: " + message);
            Debug.Log("PLAYCHECK: " + message);
        }
        IEnumerator Guarded(IEnumerator routine)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool more = false; Exception failure = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Finish(false, failure.ToString()); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
        }
        void Finish(bool success, string message)
        {
            Time.timeScale = 1;
            for(int i=0;i<8;i++) { int old=SessionState.GetInt("NailBalance.OldScore."+i,-1); if(old<0) PlayerPrefs.DeleteKey("NailBalance.Score."+i); else PlayerPrefs.SetInt("NailBalance.Score."+i,old); }
            PlayerPrefs.SetInt("NailBalance.TutorialSeen", SessionState.GetInt("NailBalance.OldTutorialPreference", 0)); PlayerPrefs.Save();
            SessionState.SetBool("NailBalance.RunValidation", false);
            evidence.Add(success ? "RESULT: PASS" : "RESULT: FAIL"); evidence.Add(message); evidence.AddRange(errors);
            File.WriteAllLines(SessionState.GetBool("NailBalance.SmokeOnly", false) ? "Logs/ui-validation.txt" : "Logs/play-validation.txt", evidence);
            Debug.Log("NAIL_BALANCE_PLAY_VALIDATION: " + (success ? "PASS" : "FAIL") + " " + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
#endif
