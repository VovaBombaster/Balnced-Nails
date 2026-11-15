using System;
using System.IO;
using NailBalance;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NailBalance.Editor
{
    // Editor-only authoring tool. The game uses the saved scene/prefabs; it never builds its UI at runtime.
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/NailBalance.unity";
        static readonly Color Ink = Hex("38413E"), Muted = Hex("7D857B"), Teal = Hex("648355");
        static readonly Color Paper = Hex("FAF9F4"), Coral = Hex("E69C7E"), White = Hex("FFFFFF");
        static Sprite round, nailSprite, square, circle, house, gear;
        static Font font;
        static Material spriteMaterial;
        static PhysicsMaterial2D friction;
        static AnimatorController pop;

        [MenuItem("Nail Balance/Rebuild authored game assets")]
        public static void CreateGame()
        {
            foreach (string folder in new[] { "Scenes", "Scripts", "Prefabs", "Sprites", "Materials", "Animations", "UI", "Puzzles" })
                Directory.CreateDirectory("Assets/" + folder);
            AssetDatabase.Refresh();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            round = MakeSprite("Rounded", 64, 64, (x, y) =>
            {
                Vector2 q = new Vector2(Mathf.Max(Mathf.Abs(x) - 0.28f, 0), Mathf.Max(Mathf.Abs(y) - 0.28f, 0));
                return new Color(1, 1, 1, Mathf.Clamp01((0.22f - q.magnitude) * 64));
            }, 64, new Vector4(16, 16, 16, 16));
            circle = MakeSprite("Circle", 128, 128, (x,y) => new Color(1,1,1,Mathf.Clamp01((0.49f - new Vector2(x,y).magnitude)*128)), 128);
            house = MakeSprite("HouseIcon", 128, 128, HousePixel, 128);
            gear = MakeSprite("SettingsIcon", 128, 128, (x,y) => {
                float r = new Vector2(x,y).magnitude;
                float edge = 0.38f + 0.065f * Mathf.Cos(Mathf.Atan2(y,x)*8);
                return new Color(1,1,1,r < edge && r > 0.16f ? 1 : 0);
            }, 128);
            square = MakeSprite("Square", 8, 8, (x, y) => Color.white, 8);
            nailSprite = MakeSprite("SteelNail", 520, 120, NailPixel, 200);
            spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            SaveAsset(spriteMaterial, "Assets/Materials/FlatSprites.mat");
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FlatSprites.mat");
            friction = new PhysicsMaterial2D("SteelGrip") { friction = 0.85f, bounciness = 0f };
            SaveAsset(friction, "Assets/Materials/SteelGrip.physicsMaterial2D");
            friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Materials/SteelGrip.physicsMaterial2D");
            pop = CreateAnimation("Popup", "m_LocalScale.x", new[] { 0.96f, 1.025f, 1f }, false, 0.25f);
            AnimationClip popClip = (AnimationClip)AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Popup.anim");
            foreach (string axis in new[] { "y", "z" })
                popClip.SetCurve("", typeof(Transform), "m_LocalScale." + axis, AnimationCurve.EaseInOut(0, 0.96f, 0.25f, 1));

            NailPiece nail = MakeNail();
            GameObject flat = MakeSupport("Flat", 0);
            GameObject dome = MakeSupport("Convex", 1);
            GameObject vee = MakeSupport("VShape", 2);
            GameObject point = MakeSupport("Point", 3);
            GameObject swing = MakeSupport("Swing", 4);
            GameObject fork = MakeSupport("Y", 5);
            PuzzleDefinition[] tasks = new PuzzleDefinition[8];
            string[] names = { "Первые гвозди", "Чуть точнее", "Тонкий баланс", "На грани", "На развилке", "Внутри V", "Разный вес", "Тихие качели" };
            string[] hints = { "Положи 3 гвоздя на опору", "Держи центр тяжести над опорой", "Чередуй направление шляпок", "Шляпки помогают удержать соседей", "Размести гвозди между ветвями Y", "Используй оба склона опоры", "Длина и масса теперь отличаются", "Опора качается — строй ближе к центру" };
            float[] widths = { 5.4f, 3.5f, 2.15f, 0.85f, 3.6f, 3.4f, 0.75f, 2.65f };
            int[] counts = { 3, 4, 5, 5, 4, 5, 5, 5 };
            GameObject[] supports = { flat, flat, flat, flat, fork, vee, point, swing };
            for (int i = 0; i < tasks.Length; i++)
            {
                PuzzleDefinition task = ScriptableObject.CreateInstance<PuzzleDefinition>();
                task.title = names[i]; task.hint = hints[i]; task.supportPrefab = supports[i];
                task.pedestalTop = i == 4 || i == 5 ? -0.47f : 0;
                task.supportWidth = widths[i]; task.timeLimit = i < 2 ? 45 : 55;
                task.background = i < 4 ? Paper : Hex("F1EEF6");
                task.supportColor = i < 4 ? Teal : Hex("8565B1");
                task.nails = new PuzzleDefinition.NailSettings[counts[i]];
                float[] lengths = { 1f, 0.88f, 1.12f, 0.94f, 1.04f };
                float[] masses = { 1f, 0.8f, 1.35f, 0.9f, 1.15f };
                float[] angles = { 90, 45, -90, -45, 90 };
                for (int n = 0; n < counts[i]; n++) task.nails[n] = new PuzzleDefinition.NailSettings {
                    length = i >= 6 ? lengths[n] : 1, mass = i >= 6 ? masses[n] : 1,
                    initialAngle = i < 2 ? 0 : i == 2 ? n % 2 * 90 : angles[n]
                };
                SaveAsset(task, $"Assets/Puzzles/Level{i / 4 + 1}_Task{i % 4 + 1}.asset");
                tasks[i] = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>($"Assets/Puzzles/Level{i / 4 + 1}_Task{i % 4 + 1}.asset");
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 8;
            camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = Paper;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.nearClipPlane = 0.1f; camera.farClipPlane = 30;
            GameController game = new GameObject("Game", typeof(GameController), typeof(NailInput)).GetComponent<GameController>();
            game.GetComponent<NailInput>().game = game;
            game.gameCamera = camera; game.nailPrefab = nail;
            game.levels = new[] {
                new GameController.Level { title = "ОСНОВЫ", tasks = new[] {tasks[0], tasks[1], tasks[2], tasks[3]} },
                new GameController.Level { title = "НОВЫЕ ОПОРЫ", tasks = new[] {tasks[4], tasks[5], tasks[6], tasks[7]} }
            };
            game.playfield = new GameObject("Playfield").transform;
            game.supportPoint = new GameObject("SupportPosition").transform; game.supportPoint.position = new Vector3(0, -1.65f, 0);
            game.spawnPoint = new GameObject("NailSpawnPosition").transform; game.spawnPoint.position = new Vector3(0, 2.35f, 0);
            WorldSprite("Pedestal", game.playfield, square, new Vector2(0, -2.8f), new Vector2(0.28f, 2.3f), Hex("A9BDBD"), -3);
            WorldSprite("Foot", game.playfield, round, new Vector2(0, -4), new Vector2(2.4f, 0.22f), Hex("A9BDBD"), -3);
            WorldSprite("GroundShadow", game.playfield, round, new Vector2(0, -4.2f), new Vector2(3.6f, 0.08f), Hex("CDDCDA"), -4);

            game.pedestal = game.playfield.Find("Pedestal");
            Canvas canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0f;
            RectTransform safe = Rect("SafeArea", canvas.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)); Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            GameUI ui = canvas.gameObject.AddComponent<GameUI>(); ui.game = game; game.ui = ui;
            CreateMenu(safe, ui, game);
            CreateHud(safe, ui, game);
            CreateLevelList(safe, ui, game);
            CreateOverlays(safe, ui, game);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            ui.Show(GameUI.ScreenKind.Menu);
            game.playfield.gameObject.SetActive(false);
            ConfigureProject();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("NAIL_BALANCE: Authored scene, five support prefabs, nail prefab, eight puzzles and all screens saved.");
        }

        static NailPiece MakeNail()
        {
            GameObject obj = new GameObject("Nail", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(NailPiece));
            SpriteRenderer sprite = obj.GetComponent<SpriteRenderer>(); sprite.sprite = nailSprite; sprite.sharedMaterial = spriteMaterial; sprite.sortingOrder = 5;
            Rigidbody2D body = obj.GetComponent<Rigidbody2D>(); body.mass = 1; body.gravityScale = 1;
            body.linearDamping = 0.12f; body.angularDamping = 0.2f; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D shaft = obj.AddComponent<BoxCollider2D>(); shaft.size = new Vector2(2.12f, 0.15f); shaft.offset = new Vector2(-0.04f, 0); shaft.sharedMaterial = friction;
            BoxCollider2D head = obj.AddComponent<BoxCollider2D>(); head.size = new Vector2(0.22f, 0.6f); head.offset = new Vector2(-1.19f, 0); head.sharedMaterial = friction;
            PolygonCollider2D tip = obj.AddComponent<PolygonCollider2D>(); tip.points = new[] { new Vector2(1, -0.075f), new Vector2(1, 0.075f), new Vector2(1.3f, 0) }; tip.sharedMaterial = friction;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, "Assets/Prefabs/Nail.prefab");
            UnityEngine.Object.DestroyImmediate(obj);
            return prefab.GetComponent<NailPiece>();
        }

        static GameObject MakeSupport(string name, int shape)
        {
            GameObject obj = new GameObject(name + "Support", typeof(Rigidbody2D));
            Rigidbody2D body = obj.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Static;
            if (shape == 1)
            {
                Vector2[] points = new Vector2[19];
                points[0] = new Vector2(-0.5f, -0.43f);
                for (int i = 0; i <= 16; i++)
                {
                    float x = -0.5f + i / 16f;
                    points[i + 1] = new Vector2(x, -0.36f + 0.36f * Mathf.Sqrt(Mathf.Max(0, 1 - x * x * 4)));
                }
                points[18] = new Vector2(0.5f, -0.43f);
                PolygonCollider2D collider = obj.AddComponent<PolygonCollider2D>(); collider.points = points; collider.sharedMaterial = friction;
                Sprite dome = MakeSprite("ConvexSupport", 256, 128, (x, y) =>
                {
                    float worldY = y * 0.5f - 0.18f;
                    return new Color(1, 1, 1, worldY <= -0.36f + 0.36f * Mathf.Sqrt(Mathf.Max(0, 1 - x * x * 4)) && worldY >= -0.43f ? 1 : 0);
                }, 256);
                WorldSprite("Surface", obj.transform, dome, new Vector2(0, -0.18f), Vector2.one, Teal, 1);
            }
            else if (shape == 5)
            {
                AddSlope(obj.transform, "YLeft", new[] {new Vector2(-0.5f,0.85f),new Vector2(0,-0.35f),new Vector2(0,-0.6f),new Vector2(-0.5f,0.6f)});
                AddSlope(obj.transform, "YRight", new[] {new Vector2(0,-0.35f),new Vector2(0.5f,0.85f),new Vector2(0.5f,0.6f),new Vector2(0,-0.6f)});
            }
            else if (shape == 2)
            {
                // A V made from two thick polygonal slopes, in normalized platform coordinates.
                AddSlope(obj.transform, "LeftSlope", new[] { new Vector2(-0.5f, 0.1f), new Vector2(0, -0.35f), new Vector2(0, -0.58f), new Vector2(-0.5f, -0.13f) });
                AddSlope(obj.transform, "RightSlope", new[] { new Vector2(0, -0.35f), new Vector2(0.5f, 0.1f), new Vector2(0.5f, -0.13f), new Vector2(0, -0.58f) });
            }
            else
            {
                BoxCollider2D box = obj.AddComponent<BoxCollider2D>(); box.size = new Vector2(1, 0.22f); box.sharedMaterial = friction;
                WorldSprite("Surface", obj.transform, square, Vector2.zero, new Vector2(1, 0.22f), Teal, 1);
                WorldSprite("TopEdge", obj.transform, square, new Vector2(0, 0.08f), new Vector2(1, 0.035f), Teal, 2);
                if (shape == 4)
                {
                    body.bodyType = RigidbodyType2D.Dynamic; body.mass = 8; body.angularDamping = 0.6f;
                    HingeJoint2D joint = obj.AddComponent<HingeJoint2D>(); joint.autoConfigureConnectedAnchor = false;
                    joint.connectedAnchor = Vector2.zero; joint.useLimits = true;
                    joint.limits = new JointAngleLimits2D { min = -6, max = 6 };
                    joint.useMotor = true; joint.motor = new JointMotor2D { maxMotorTorque = 35, motorSpeed = 4 };
                    obj.AddComponent<SwingSupport>();
                    WorldSprite("Pivot", obj.transform, round, new Vector2(0, -0.18f), new Vector2(0.06f, 0.16f), Ink, 3);
                }
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, $"Assets/Prefabs/{name}Support.prefab");
            UnityEngine.Object.DestroyImmediate(obj); return prefab;
        }

        static void AddSlope(Transform parent, string name, Vector2[] points)
        {
            GameObject obj = new GameObject(name); obj.transform.SetParent(parent, false);
            PolygonCollider2D collider = obj.AddComponent<PolygonCollider2D>(); collider.points = points; collider.sharedMaterial = friction;
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 v in points) { min = Vector2.Min(min,v); max = Vector2.Max(max,v); }
            Vector2 center = (min + max) * 0.5f, size = max - min;
            Sprite sprite = MakeSprite(name, 256, 256, (x, y) =>
            {
                Vector2 p = center + new Vector2(x * size.x, y * size.y);
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    if ((points[i].y > p.y) != (points[j].y > p.y) && p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x) inside = !inside;
                return new Color(1, 1, 1, inside ? 1 : 0);
            }, 256);
            WorldSprite("Surface", obj.transform, sprite, center, size, Teal, 1);
        }

        static Button HomeIcon(string name, Transform parent, Vector2 position, UnityAction action)
        {
            Button button = Button(name, parent, "", position, new Vector2(128,128), White, Ink, action);
            button.image.sprite = circle; button.image.type = Image.Type.Simple;
            Picture("House", button.transform, house, Vector2.zero, new Vector2(72,72), Ink);
            return button;
        }

        static void CreateMenu(Transform parent, GameUI ui, GameController game)
        {
            ui.menu = FullPanel("MainMenu", parent, Paper);
            Transform p = ui.menu.transform;
            for (int i=0;i<14;i++)
            {
                float x = i % 2 == 0 ? -440 : 440;
                Image speck = Picture("BackgroundNail"+i,p,nailSprite,new Vector2(x,760-i/2*250),new Vector2(95,22),new Color(.55f,.58f,.55f,.3f));
                speck.rectTransform.localRotation=Quaternion.Euler(0,0,45+i*37);
            }
            Label("Title",p,"БАЛАНС ГВОЗДЕЙ",new Vector2(0,765),new Vector2(860,90),55,Ink,FontStyle.Bold);
            ui.menuScore=Label("TotalScore",p,"Всего очков: 0",new Vector2(0,655),new Vector2(800,70),36,Teal,FontStyle.Bold);
            for(int i=0;i<3;i++)
            {
                Image n=Picture("LogoNail"+i,p,nailSprite,new Vector2(-130+i*130,390),new Vector2(380,88),White);
                n.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
            }
            Picture("LogoCrossNail",p,nailSprite,new Vector2(0,390),new Vector2(530,122),White).rectTransform.localRotation=Quaternion.Euler(0,0,195);
            Button("PlayButton",p,"ИГРАТЬ",new Vector2(0,65),new Vector2(790,135),Hex("C8DDA6"),Ink,game.StartGame,52);
            Button("LevelsButton",p,"УРОВНИ",new Vector2(0,-145),new Vector2(790,135),Hex("F5E5A3"),Ink,game.OpenLevels,48);
            for(int i=0;i<4;i++) Picture("Grid"+i,p,square,new Vector2(-306+i%2*28,-131-i/2*28),new Vector2(18,18),Ink);
            Button settings=Button("SettingsButton",p,"НАСТРОЙКИ",new Vector2(0,-355),new Vector2(790,135),Hex("E5E5DE"),Ink,null,43);
            Picture("Gear",settings.transform,gear,new Vector2(-305,0),new Vector2(74,74),Ink);
            Button("HelpButton",p,"Как играть",new Vector2(0,-520),new Vector2(360,70),Paper,Muted,game.OpenTutorial,30);
            Button("QuitButton",p,"ВЫХОД",new Vector2(0,-745),new Vector2(670,115),Hex("E8BFBA"),Ink,game.QuitGame,43);
        }

        static void CreateLevelList(Transform parent, GameUI ui, GameController game)
        {
            ui.levelSelect=FullPanel("LevelSelectScreen",parent,Paper);
            Transform p=ui.levelSelect.transform;
            Label("Title",p,"УРОВНИ",new Vector2(0,790),new Vector2(650,100),65,Ink,FontStyle.Bold);
            Button("BackButton",p,"←",new Vector2(-430,790),new Vector2(115,100),Paper,Ink,game.Home,60);
            Label("Hint",p,"Зелёные — пройдены. Жёлтая — следующая задача.",new Vector2(0,670),new Vector2(900,95),30,Muted);
            ui.taskButtons=new Button[8]; ui.taskButtonLabels=new Text[8]; ui.levelScoreLabels=new Text[2];
            for(int level=0;level<2;level++)
            {
                float y=495-level*650;
                Label("Level"+level,p,"УРОВЕНЬ "+(level+1),new Vector2(0,y),new Vector2(800,80),49,Ink,FontStyle.Bold);
                ui.levelScoreLabels[level]=Label("LevelScore"+level,p,"0 очков",new Vector2(0,y-50),new Vector2(800,45),27,Muted);
                for(int task=0;task<4;task++)
                {
                    int index=level*4+task;
                    Button button=Button("TaskButton"+index,p,"",new Vector2(task%2==0?-215:215,y-160-task/2*210),new Vector2(390,165),White,Ink,null,36);
                    ColorBlock colors=button.colors; colors.disabledColor=Color.white; button.colors=colors;
                    UnityEventTools.AddIntPersistentListener(button.onClick,game.SelectTask,index);
                    ui.taskButtons[index]=button; ui.taskButtonLabels[index]=button.GetComponentInChildren<Text>();
                }
            }
            Label("ReplayHint",p,"Повторное прохождение — без новых очков",new Vector2(0,-780),new Vector2(900,80),29,Muted);
            HomeIcon("HomeButton",p,new Vector2(0,-885),game.Home);
        }

        static void CreateHud(Transform parent, GameUI ui, GameController game)
        {
            ui.hud = FullPanel("GameHUD", parent, Color.clear, false);
            Transform p = ui.hud.transform;
            RectTransform top = Rect("TopHUD",p,new Vector2(0,-135),new Vector2(1080,270),new Vector2(.5f,1));
            Picture("TopPaper",top,square,Vector2.zero,new Vector2(1080,270),Paper);
            Picture("Divider",top,square,new Vector2(0,-135),new Vector2(1080,3),Ink);
            Button("BackButton",top,"←",new Vector2(-425,35),new Vector2(110,110),Paper,Ink,game.OpenLevels,65);
            ui.levelLabel=Label("Level",top,"УРОВЕНЬ 1",new Vector2(0,45),new Vector2(650,90),59,Ink,FontStyle.Bold);
            Picture("Settings",top,gear,new Vector2(425,35),new Vector2(88,88),Muted);
            ui.taskLabel=Label("Task",top,"Задача 1 / 4",new Vector2(-300,-75),new Vector2(350,60),31,Muted);
            ui.scoreLabel=Label("Score",top,"0 очков",new Vector2(80,-75),new Vector2(380,60),36,Ink,FontStyle.Bold);
            Button pause=Button("PauseButton",top,"",new Vector2(405,-75),new Vector2(160,66),Paper,Ink,game.Pause,28);
            ui.timeLabel=pause.GetComponentInChildren<Text>();
            ui.taskDots=new Image[0];
            RectTransform status=Rect("Status",p,new Vector2(0,-335),new Vector2(940,90),new Vector2(.5f,1));
            ui.statusLabel=Label("StatusText",status,"",new Vector2(0,15),new Vector2(940,55),29,Muted);
            Picture("StabilityTrack",status,round,new Vector2(0,-28),new Vector2(430,10),Hex("E3E5DC"));
            ui.stabilityFill=Picture("StabilityFill",status,square,new Vector2(0,-28),new Vector2(430,10),Teal);
            ui.stabilityFill.type=Image.Type.Filled; ui.stabilityFill.fillMethod=Image.FillMethod.Horizontal; ui.stabilityFill.fillAmount=0;
            RectTransform bottom=Rect("Controls",p,new Vector2(0,180),new Vector2(1080,360),new Vector2(.5f,0));
            Picture("BottomPaper",bottom,square,new Vector2(0,-35),new Vector2(1080,290),Paper);
            Picture("Divider",bottom,square,new Vector2(0,110),new Vector2(1080,3),Ink);
            ui.angleLabel=Label("Angle",bottom,"Угол 0°",new Vector2(0,220),new Vector2(850,50),25,Muted);
            RectTransform sliderRoot = Rect("AngleSlider", bottom, new Vector2(0, 165), new Vector2(820, 75), Vector2.one * 0.5f);
            Image hit = sliderRoot.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Slider slider = sliderRoot.gameObject.AddComponent<Slider>(); slider.minValue = -180; slider.maxValue = 180;
            Picture("Track", sliderRoot, round, Vector2.zero, new Vector2(820, 10), Hex("DFE8E8"));
            RectTransform handleArea = Rect("HandleArea", sliderRoot, Vector2.zero, new Vector2(-44, 52), Vector2.one * 0.5f);
            handleArea.anchorMin = new Vector2(0, 0.5f); handleArea.anchorMax = new Vector2(1, 0.5f);
            Image handle = Picture("Handle", handleArea, round, Vector2.zero, new Vector2(52, 52), Teal); handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(52, 0);
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, ui.ChangeAngle); ui.angleSlider = slider;
            HomeIcon("HomeButton",bottom,new Vector2(-390,-20),game.Home);
            ui.dropButton=Button("DropButton",bottom,"СБРОС",new Vector2(0,-20),new Vector2(415,135),Hex("F5E5A3"),Ink,game.DropNail,52);
            Image count=Picture("RemainingCircle",bottom,circle,new Vector2(390,-20),new Vector2(128,128),White);
            Outline outline=count.gameObject.AddComponent<Outline>(); outline.effectColor=Ink; outline.effectDistance=new Vector2(3,-3);
            ui.nailsLabel=Label("RemainingNails",count.transform,"3",Vector2.zero,new Vector2(120,100),65,Ink);
            Label("RemainingCaption",bottom,"осталось",new Vector2(390,-117),new Vector2(200,45),23,Muted);
            Label("SwipeHint",bottom,"Касание: поворот 90° · Свайп вниз: сброс",new Vector2(-70,-142),new Vector2(740,38),23,Muted);
        }

        static void CreateOverlays(Transform parent, GameUI ui, GameController game)
        {
            Transform card;
            ui.pause = Overlay("PauseScreen", parent, "Пауза", "Передохни. Баланс подождёт.", Teal, out card);
            Button("ResumeButton", card, "Продолжить", new Vector2(0, 10), new Vector2(730, 105), Teal, White, game.Resume);
            Button("RetryButton", card, "Повторить", new Vector2(0, -120), new Vector2(730, 100), Hex("E5EFED"), Ink, game.Retry);
            Button("HomeButton", card, "Домой", new Vector2(0, -250), new Vector2(730, 100), Hex("E5EFED"), Ink, game.Home);
            ui.failure = Overlay("FailureScreen", parent, "Не получилось!", "", Hex("BE5B58"), out card);
            ui.failureReason = Label("Reason", card, "Гвоздь упал с опоры.", new Vector2(0, 108), new Vector2(800, 95), 32, Muted);
            Button("RetryButton", card, "Повторить", new Vector2(0, -80), new Vector2(730, 110), Hex("BE5B58"), White, game.Retry);
            Button("HomeButton", card, "Домой", new Vector2(0, -230), new Vector2(730, 100), Hex("F4E9E5"), Ink, game.Home);
            ui.success = Overlay("SuccessScreen", parent, "ПОБЕДА!", "Конструкция держится", Teal, out card);
            ui.successScore = Label("TaskScore", card, "+100 очков", new Vector2(0, -20), new Vector2(800, 170), 40, Teal, FontStyle.Bold);
            HomeIcon("HomeButton",card,new Vector2(-320,-235),game.Home);
            ui.nextButton=Button("NextTaskButton",card,"Далее →",new Vector2(130,-235),new Vector2(520,125),Hex("C8DDA6"),Ink,game.Advance,40);
            ui.levelComplete = Overlay("LevelCompleteScreen", parent, "Уровень 1 пройден!", "", Teal, out card);
            ui.levelScore = Label("LevelScore", card, "4 задачи решены", new Vector2(0, 85), new Vector2(800, 130), 38, Muted);
            Button("NextLevelButton", card, "Следующий уровень", new Vector2(0, -90), new Vector2(730, 110), Teal, White, game.NextLevel, 36);
            Button("HomeButton", card, "Домой", new Vector2(0, -235), new Vector2(730, 100), Hex("E5EFED"), Ink, game.Home);
            ui.complete = Overlay("GameCompleteScreen", parent, "Игра пройдена!", "", Hex("8565B1"), out card);
            ui.finalScore = Label("FinalScore", card, "8 задач · 2 уровня", new Vector2(0, 80), new Vector2(800, 150), 40, Muted);
            Button("RestartButton", card, "Выбрать задачу", new Vector2(0, -90), new Vector2(730, 110), Hex("8565B1"), White, game.OpenLevels, 38);
            Button("HomeButton", card, "Домой", new Vector2(0, -235), new Vector2(730, 100), Hex("EEE8F4"), Ink, game.Home);

            ui.tutorial = FullPanel("TutorialScreen", parent, new Color(Ink.r, Ink.g, Ink.b, 0.72f));
            Image tutorialCard = Picture("TutorialCard", ui.tutorial.transform, round, Vector2.zero, new Vector2(960, 870), White);
            tutorialCard.gameObject.AddComponent<Animator>().runtimeAnimatorController = pop; tutorialCard.GetComponent<Animator>().updateMode = AnimatorUpdateMode.UnscaledTime;
            card = tutorialCard.transform;
            Label("Title", card, "Три простых движения", new Vector2(0, 330), new Vector2(880, 90), 49, Ink, FontStyle.Bold);
            string[] labels = { "Двигай ↔", "Касайся: 90°", "Свайп вниз ↓" };
            for (int i = 0; i < 3; i++)
            {
                Transform tile = Picture("Gesture" + i, card, round, new Vector2(-300 + i * 300, 70), new Vector2(274, 300), Hex("EDF5F3")).transform;
                Image icon = Picture("AnimatedNail", tile, nailSprite, new Vector2(0, 32), new Vector2(180, 42), White);
                string property = i == 0 ? "m_AnchoredPosition.x" : i == 1 ? "localEulerAnglesRaw.z" : "m_AnchoredPosition.y";
                float[] values = i == 0 ? new[] {-30f, 30f, -30f} : i == 1 ? new[] {0f, 90f, 0f} : new[] {62f, -30f, 62f};
                var animator = icon.gameObject.AddComponent<Animator>(); animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.runtimeAnimatorController = CreateAnimation("Tutorial" + i, property, values, true, 2.4f, i != 1);
                Label("Caption", tile, labels[i], new Vector2(0, -100), new Vector2(270, 65), 27, Ink, FontStyle.Bold);
            }
            Label("FineAngleHint", card, "Нужен точный угол? Двигай ползунок.\nУдержи все гвозди на опоре 3 секунды.", new Vector2(0, -165), new Vector2(860, 125), 31, Muted);
            Button("UnderstoodButton", card, "Понятно", new Vector2(0, -325), new Vector2(780, 110), Teal, White, game.CloseTutorial, 40);
        }

        static GameObject Overlay(string name, Transform parent, string title, string subtitle, Color accent, out Transform card)
        {
            GameObject root = FullPanel(name, parent, new Color(Ink.r, Ink.g, Ink.b, 0.68f));
            Image panel = Picture("Card", root.transform, round, Vector2.zero, new Vector2(920, 780), White);
            card = panel.transform;
            Animator animator = panel.gameObject.AddComponent<Animator>(); animator.runtimeAnimatorController = pop; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            Picture("Accent", card, round, new Vector2(0, 350), new Vector2(150, 10), accent);
            Label("Title", card, title, new Vector2(0, 250), new Vector2(850, 100), 51, accent, FontStyle.Bold);
            Label("Subtitle", card, subtitle, new Vector2(0, 133), new Vector2(820, 65), 32, Muted);
            return root;
        }

        static Text Counter(Transform parent, string name, string caption, Vector2 position)
        {
            Image card = Picture(name + "Card", parent, round, position, new Vector2(300, 116), White);
            Label(name + "Caption", card.transform, caption, new Vector2(0, 30), new Vector2(280, 35), 22, Muted, FontStyle.Bold);
            return Label(name + "Value", card.transform, "0", new Vector2(0, -20), new Vector2(280, 59), 43, Ink, FontStyle.Bold);
        }

        static GameObject FullPanel(string name, Transform parent, Color color, bool block = true)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.one * 0.5f); Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = block;
            return rect.gameObject;
        }

        static Button Button(string name, Transform parent, string caption, Vector2 pos, Vector2 size, Color bg, Color fg, UnityAction action, int fontSize = 38)
        {
            Image image = Picture(name, parent, round, pos, size, bg); image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(0.93f, 0.97f, 0.97f); colors.pressedColor = new Color(0.78f, 0.87f, 0.87f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            Outline outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(2, -2);
            Label("Label", image.transform, caption, Vector2.zero, size - new Vector2(16, 8), fontSize, fg, FontStyle.Bold);
            return button;
        }

        static Text Label(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(name, parent, pos, size, Vector2.one * 0.5f);
            Text label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = fontSize;
            label.color = color; label.fontStyle = style; label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        static Image Picture(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent, pos, size, Vector2.one * 0.5f);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false;
            image.type = sprite == round ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = pos; rect.sizeDelta = size;
            return rect;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void WorldSprite(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 scale, Color color, int order)
        {
            GameObject obj = new GameObject(name, typeof(SpriteRenderer)); obj.transform.SetParent(parent, false); obj.transform.localPosition = pos;
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1);
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order; renderer.sharedMaterial = spriteMaterial;
        }

        static Sprite MakeSprite(string name, int width, int height, Func<float, float, Color> pixel, float pixelsPerUnit, Vector4 border = default)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) colors[y * width + x] = pixel((x + 0.5f) / width - 0.5f, (y + 0.5f) / height - 0.5f);
            texture.SetPixels(colors); texture.Apply();
            string path = "Assets/Sprites/" + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit; importer.spriteBorder = border; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Color HousePixel(float x, float y)
        {
            Vector2 p = new Vector2(x,y);
            Vector2[] path = {new Vector2(-.42f,0),new Vector2(0,.38f),new Vector2(.42f,0),new Vector2(.3f,0),new Vector2(.3f,-.35f),new Vector2(.08f,-.35f),new Vector2(.08f,-.1f),new Vector2(-.08f,-.1f),new Vector2(-.08f,-.35f),new Vector2(-.3f,-.35f),new Vector2(-.3f,0),new Vector2(-.42f,0)};
            for(int i=1;i<path.Length;i++) { Vector2 d=path[i]-path[i-1]; float t=Mathf.Clamp01(Vector2.Dot(p-path[i-1],d)/d.sqrMagnitude); if(Vector2.Distance(p,path[i-1]+d*t)<.035f) return Color.white; }
            return Color.clear;
        }

        static Color NailPixel(float u, float v)
        {
            float x = u * 2.6f, y = v * 0.6f;
            Color dark = Hex("435662"), steel = Hex("A9BBC3"), light = Hex("DDE8EA");
            if (x < -1.08f) return Mathf.Abs(y) > 0.275f || x < -1.275f || x > -1.105f ? dark : y > 0.15f ? light : steel;
            if (x <= 1f && Mathf.Abs(y) <= 0.075f) return Mathf.Abs(y) > 0.057f ? dark : y > 0.012f ? light : steel;
            if (x > 1f && Mathf.Abs(y) < 0.075f * (1.3f - x) / 0.3f) return y > 0 ? light : dark;
            return Color.clear;
        }

        static AnimatorController CreateAnimation(string name, string property, float[] values, bool loop, float duration, bool rect = false)
        {
            AnimationClip clip = new AnimationClip { name = name, frameRate = 30 };
            Keyframe[] keys = new Keyframe[values.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(i * duration / (keys.Length - 1), values[i]);
            clip.SetCurve("", rect ? typeof(RectTransform) : typeof(Transform), property, new AnimationCurve(keys));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            string clipPath = "Assets/Animations/" + name + ".anim"; SaveAsset(clip, clipPath);
            string path = "Assets/Animations/" + name + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in machine.states) machine.RemoveState(state.state);
            AnimatorState animation = machine.AddState(name); animation.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath); machine.defaultState = animation;
            return controller;
        }
        static void SaveAsset(UnityEngine.Object asset, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing) { EditorUtility.CopySerialized(asset, existing); UnityEngine.Object.DestroyImmediate(asset); }
            else AssetDatabase.CreateAsset(asset, path);
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }

        static void ConfigureProject()
        {
            PlayerSettings.companyName = "Student Studio"; PlayerSettings.productName = "Баланс гвоздей";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.student.nailbalance");
            PlayerSettings.bundleVersion = "1.0"; PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false; PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultIsNativeResolution = false; PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Time.fixedDeltaTime = 0.02f; Physics2D.velocityIterations = 12; Physics2D.positionIterations = 8;
            Physics2D.gravity = new Vector2(0, -9.81f);
        }

        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/NailBalance.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("NAIL_BALANCE: Windows build succeeded.");
        }
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Android/NailBalance.apk", BuildTarget.Android, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed: " + report.summary.result);
            Debug.Log("NAIL_BALANCE: Android APK build succeeded.");
        }

        [MenuItem("Nail Balance/Open ready scene")]
        public static void OpenForReview()
        {
            SessionState.SetBool("NailBalance.RunValidation", false);
            EditorSceneManager.OpenScene(ScenePath);
            PlayerPrefs.SetInt("NailBalance.TutorialSeen", 0);
            PlayerPrefs.Save();
            EditorApplication.delayCall += () => PlayValidation.SetGameSize(540, 960);
        }
    }
}
