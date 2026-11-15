using System.Collections.Generic;
using UnityEngine;

namespace NailBalance
{
    public class GameController : MonoBehaviour
    {
        [System.Serializable]
        public class Level
        {
            public string title;
            public PuzzleDefinition[] tasks;
        }
        public enum Phase { Menu, LevelSelect, Playing, Tutorial, Paused, Success, Failure, LevelComplete, Complete }
        [Header("Two levels, four tasks each")]
        public Level[] levels;
        [Header("Scene and prefab references")]
        public NailPiece nailPrefab;
        public Transform playfield, supportPoint, spawnPoint;
        public Transform pedestal;
        public Camera gameCamera;
        public GameUI ui;
        [Header("Physics rules")]
        public float fallLine = -4.6f;
        public float horizontalLimit = 3.1f;
        public float nextNailDelay = 0.65f;
        public Phase State { get; private set; }
        public int LevelIndex { get; private set; }
        public int TaskIndex { get; private set; }
        public int Score { get; private set; }
        public int StageIndex => LevelIndex * 4 + TaskIndex;
        public int[] TaskScores { get; private set; } = new int[8];
        public int NextTask
        {
            get { for (int i = 0; i < TaskScores.Length; i++) if (TaskScores[i] == 0) return i; return 8; }
        }
        public int DroppedCount => dropped.Count;
        public float RemainingTime { get; private set; }
        public float StableTime { get; private set; }
        public NailPiece HeldNail { get; private set; }
        public PuzzleDefinition CurrentPuzzle => levels[LevelIndex].tasks[TaskIndex];
        public bool CanControl => State == Phase.Playing && HeldNail;
        public Transform Support => support;
        readonly List<NailPiece> dropped = new List<NailPiece>();
        readonly HashSet<Rigidbody2D> supported = new HashSet<Rigidbody2D>();
        Transform support;
        float spawnDelay, resultDelay;
        bool tutorialFromMenu;

        void Start()
        {
            Application.targetFrameRate = 60;
            LoadProgress();
            Home();
        }

        public void StartGame()
        {
            SelectTask(NextTask < 8 ? NextTask : 0);
        }

        public void LoadProgress()
        {
            Score = 0;
            for (int i = 0; i < TaskScores.Length; i++)
            {
                TaskScores[i] = Mathf.Max(0, PlayerPrefs.GetInt("NailBalance.Score." + i, 0));
                Score += TaskScores[i];
            }
        }

        public void SelectTask(int index)
        {
            if (index < 0 || index >= 8 || index > NextTask) return;
            LevelIndex = index / 4; TaskIndex = index % 4;
            LoadTask();
            if (PlayerPrefs.GetInt("NailBalance.TutorialSeen", 0) == 0) OpenTutorial();
        }

        public void OpenLevels()
        {
            ClearBoard();
            playfield.gameObject.SetActive(false);
            State = Phase.LevelSelect;
            Time.timeScale = 1;
            ui.RefreshProgress();
            ui.Show(GameUI.ScreenKind.LevelSelect);
        }

        void LoadTask()
        {
            ClearBoard();
            playfield.gameObject.SetActive(true);
            PuzzleDefinition puzzle = CurrentPuzzle;
            float top = supportPoint.position.y + puzzle.pedestalTop;
            pedestal.localPosition = new Vector3(0, (top - 4f) * 0.5f, 0);
            pedestal.localScale = new Vector3(0.28f, top + 4f, 1);
            gameCamera.backgroundColor = puzzle.background;
            support = Instantiate(puzzle.supportPrefab, supportPoint.position, Quaternion.identity, playfield).transform;
            support.localScale = new Vector3(puzzle.supportWidth, 1, 1);
            foreach (SpriteRenderer sprite in support.GetComponentsInChildren<SpriteRenderer>())
                if (sprite.gameObject.name != "Pivot") sprite.color = puzzle.supportColor;
            RemainingTime = puzzle.timeLimit;
            StableTime = spawnDelay = 0;
            State = Phase.Playing;
            Time.timeScale = 1;
            SpawnNail();
            ui.Show(GameUI.ScreenKind.Game);
            ui.Refresh();
        }

        void SpawnNail()
        {
            HeldNail = Instantiate(nailPrefab, spawnPoint.position, Quaternion.identity, playfield);
            HeldNail.Prepare(CurrentPuzzle.nails[dropped.Count]);
        }

        public void MoveNail(float x)
        {
            if (!CanControl) return;
            float visibleLimit = gameCamera.orthographicSize * gameCamera.aspect - 1.6f;
            float limit = Mathf.Min(horizontalLimit, Mathf.Max(0.5f, visibleLimit));
            Vector3 position = HeldNail.transform.position;
            position.x = Mathf.Clamp(x, -limit, limit);
            HeldNail.transform.position = position;
        }

        public void SetAngle(float angle)
        {
            if (CanControl) HeldNail.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        public void RotateNail() { if (CanControl) SetAngle(HeldNail.transform.eulerAngles.z + 90); }

        public void DropNail()
        {
            if (!CanControl) return;
            HeldNail.Release();
            dropped.Add(HeldNail);
            HeldNail = null;
            spawnDelay = nextNailDelay;
            ui.Refresh();
        }

        void Update()
        {
            if (State == Phase.Success)
            {
                resultDelay -= Time.unscaledDeltaTime;
                ui.nextButton.interactable = resultDelay <= 0;
                return;
            }
            if (State != Phase.Playing) return;
            RemainingTime = Mathf.Max(0, RemainingTime - Time.deltaTime);
            foreach (NailPiece nail in dropped)
                if (nail.transform.position.y < fallLine) { Lose("Гвоздь упал с опоры."); return; }
            if (RemainingTime <= 0) { Lose("Время вышло. Попробуем ещё раз?"); return; }
            if (!HeldNail && dropped.Count < CurrentPuzzle.nails.Length)
            {
                spawnDelay -= Time.deltaTime;
                if (spawnDelay <= 0) SpawnNail();
            }
            ui.Refresh();
        }

        void FixedUpdate()
        {
            if (State != Phase.Playing || dropped.Count != CurrentPuzzle.nails.Length) return;
            StableTime = IsConstructionStable(dropped, support, supported) ? StableTime + Time.fixedDeltaTime : 0;
            if (StableTime >= 3f) Win();
        }

        public static bool IsConstructionStable(List<NailPiece> nails, Transform platform, HashSet<Rigidbody2D> connected)
        {
            connected.Clear();
            // Expand the contact chain from the platform through the stack.
            for (int pass = 0; pass < nails.Count; pass++)
                foreach (NailPiece nail in nails)
                    if (!connected.Contains(nail.Body) && nail.TouchesSupport(platform, connected)) connected.Add(nail.Body);
            if (connected.Count != nails.Count) return false;
            foreach (NailPiece nail in nails) if (!nail.IsQuiet) return false;
            return true;
        }

        void Win()
        {
            int bonus = Mathf.FloorToInt(RemainingTime) * 2;
            int earned = TaskScores[StageIndex] == 0 ? 100 + bonus : 0;
            if (earned > 0)
            {
                TaskScores[StageIndex] = earned;
                Score += earned;
                PlayerPrefs.SetInt("NailBalance.Score." + StageIndex, earned);
                PlayerPrefs.Save();
            }
            State = Phase.Success;
            Time.timeScale = 0;
            resultDelay = 2f;
            ui.successScore.text = earned > 0 ? $"+{earned} очков\nВсего: {Score}" : $"Повторное прохождение\n+0 очков · Всего: {Score}";
            ui.nextButton.interactable = false;
            ui.Refresh();
            ui.Show(GameUI.ScreenKind.Success);
        }

        void Lose(string reason)
        {
            State = Phase.Failure;
            Time.timeScale = 0;
            ui.failureReason.text = reason;
            ui.Show(GameUI.ScreenKind.Failure);
        }

        public void Advance()
        {
            if (State != Phase.Success || resultDelay > 0) return;
            if (TaskIndex < 3) { TaskIndex++; LoadTask(); return; }
            if (LevelIndex == 0)
            {
                State = Phase.LevelComplete;
                ui.levelScore.text = $"4 задачи решены\n{Score} очков";
                ui.Show(GameUI.ScreenKind.LevelComplete);
            }
            else
            {
                State = Phase.Complete;
                ui.finalScore.text = $"8 задач · 2 уровня\n{Score} очков";
                ui.Show(GameUI.ScreenKind.Complete);
            }
        }

        public void NextLevel()
        {
            if (State != Phase.LevelComplete) return;
            LevelIndex = 1;
            TaskIndex = 0;
            LoadTask();
        }
        public void Retry() { if (State == Phase.Failure || State == Phase.Paused) LoadTask(); }
        public void Pause()
        {
            if (State != Phase.Playing) return;
            State = Phase.Paused;
            Time.timeScale = 0;
            ui.Show(GameUI.ScreenKind.Pause);
        }
        public void Resume()
        {
            if (State != Phase.Paused) return;
            State = Phase.Playing;
            Time.timeScale = 1;
            ui.Show(GameUI.ScreenKind.Game);
        }
        public void Home()
        {
            ClearBoard();
            State = Phase.Menu;
            Time.timeScale = 1;
            playfield.gameObject.SetActive(false);
            gameCamera.backgroundColor = levels[0].tasks[0].background;
            ui.Show(GameUI.ScreenKind.Menu);
            ui.RefreshProgress();
        }
        public void QuitGame()
        {
            PlayerPrefs.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        public void OpenTutorial()
        {
            tutorialFromMenu = State == Phase.Menu;
            State = Phase.Tutorial;
            Time.timeScale = 0;
            ui.Show(GameUI.ScreenKind.Tutorial);
        }
        public void CloseTutorial()
        {
            PlayerPrefs.SetInt("NailBalance.TutorialSeen", 1);
            PlayerPrefs.Save();
            if (tutorialFromMenu) Home();
            else { State = Phase.Paused; Resume(); }
        }
        void ClearBoard()
        {
            foreach (NailPiece nail in dropped) { nail.gameObject.SetActive(false); Destroy(nail.gameObject); }
            dropped.Clear();
            if (HeldNail) { HeldNail.gameObject.SetActive(false); Destroy(HeldNail.gameObject); }
            if (support) { support.gameObject.SetActive(false); Destroy(support.gameObject); }
            HeldNail = null;
            support = null;
        }
        void OnApplicationPause(bool paused) { if (paused) Pause(); }
        void OnDestroy() => Time.timeScale = 1;
    }
}
