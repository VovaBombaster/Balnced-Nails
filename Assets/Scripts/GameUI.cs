using UnityEngine;
using UnityEngine.UI;

namespace NailBalance
{
    public class GameUI : MonoBehaviour
    {
        public enum ScreenKind { Menu, LevelSelect, Game, Tutorial, Pause, Failure, Success, LevelComplete, Complete }
        [Header("Saved screen objects")]
        public GameObject menu, hud, tutorial, pause, failure, success, levelComplete, complete;
        public GameObject levelSelect;
        public Text menuScore;
        public Button[] taskButtons;
        public Text[] taskButtonLabels;
        public Text[] levelScoreLabels;
        public Button nextButton;
        [Header("Game HUD")]
        public Text levelLabel, taskLabel, nailsLabel, timeLabel, scoreLabel, statusLabel, angleLabel;
        public Slider angleSlider;
        public Button rotateButton, dropButton;
        public Image stabilityFill;
        public Image[] taskDots;
        [Header("Results")]
        public Text failureReason, successScore, levelScore, finalScore;
        public GameController game;
        static readonly Color Ink = new Color(0.13f, 0.23f, 0.29f);

        public void Show(ScreenKind screen)
        {
            menu.SetActive(screen == ScreenKind.Menu);
            hud.SetActive(screen != ScreenKind.Menu && screen != ScreenKind.LevelSelect);
            levelSelect.SetActive(screen == ScreenKind.LevelSelect);
            tutorial.SetActive(screen == ScreenKind.Tutorial);
            pause.SetActive(screen == ScreenKind.Pause);
            failure.SetActive(screen == ScreenKind.Failure);
            success.SetActive(screen == ScreenKind.Success);
            levelComplete.SetActive(screen == ScreenKind.LevelComplete);
            complete.SetActive(screen == ScreenKind.Complete);
        }

        public void Refresh()
        {
            PuzzleDefinition puzzle = game.CurrentPuzzle;
            if (!puzzle) return;
            levelLabel.text = $"УРОВЕНЬ {game.LevelIndex + 1}";
            taskLabel.text = $"Задача {game.TaskIndex + 1} / 4";
            nailsLabel.text = (puzzle.nails.Length - game.DroppedCount).ToString();
            timeLabel.text = $"{Mathf.CeilToInt(game.RemainingTime)} с · II";
            timeLabel.color = game.RemainingTime <= 10 ? new Color(0.8f, 0.24f, 0.24f) : Ink;
            scoreLabel.text = $"{game.Score} очков";
            stabilityFill.fillAmount = game.StableTime / 3f;
            statusLabel.text = game.DroppedCount == puzzle.nails.Length
                ? (game.StableTime > 0 ? $"Держится!  {game.StableTime:0.0} / 3 с" : "Ждём равновесия…")
                : puzzle.hint;
            for (int i = 0; i < taskDots.Length; i++)
                taskDots[i].color = i <= game.TaskIndex ? puzzle.supportColor : new Color(0.80f, 0.84f, 0.85f);
            bool canControl = game.CanControl;
            angleSlider.interactable = dropButton.interactable = canControl;
            if (rotateButton) rotateButton.interactable = canControl;
            float angle = game.HeldNail ? Mathf.DeltaAngle(0, game.HeldNail.transform.eulerAngles.z) : 0;
            angleSlider.SetValueWithoutNotify(angle);
            angleLabel.text = $"ТОЧНЫЙ УГОЛ   {Mathf.RoundToInt(angle)}°";
        }

        public void ChangeAngle(float angle) => game.SetAngle(angle);

        public void RefreshProgress()
        {
            menuScore.text = $"Всего очков: {game.Score}";
            for (int level = 0; level < 2; level++)
            {
                int total = 0;
                for (int task = 0; task < 4; task++) total += game.TaskScores[level * 4 + task];
                levelScoreLabels[level].text = $"{total} очков";
            }
            for (int i = 0; i < taskButtons.Length; i++)
            {
                bool completed = game.TaskScores[i] > 0;
                taskButtons[i].interactable = i <= game.NextTask;
                taskButtons[i].image.color = completed ? new Color(0.67f, 0.83f, 0.57f)
                    : i == game.NextTask ? new Color(0.98f, 0.89f, 0.56f) : new Color(0.88f, 0.88f, 0.86f);
                taskButtonLabels[i].text = $"Задача {i % 4 + 1}\n" + (completed ? $"{game.TaskScores[i]} очков" : i == game.NextTask ? "Играть" : "Закрыто");
            }
        }
    }
}
