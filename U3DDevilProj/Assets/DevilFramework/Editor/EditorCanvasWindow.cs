﻿using LitJson;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevilEditor
{

    public abstract class EditorCanvasWindow : EditorWindow
    {
        public static float deltaTime { get; private set; }

        public class DelayTask
        {
            public int Id { get; private set; }
            public System.Action Act { get; set; }
            public DelayTask(int id, System.Action act)
            {
                this.Id = id;
                this.Act = act;
            }
        }

        bool focusCenter;
        Vector2 mousePos;
        Vector2 mouseDeltaPos;
        bool onFocus;
        Rect cachedViewportRect;
        Rect clipRect;
        public bool InterceptMouse { get; private set; }

        EMouseAction mouseAction;
        EMouseButton mouseButton;
        protected string statInfo = "";

        readonly float mUpdateDeltaTime = 0.05f;
        readonly float mUpdateDeltaTimeEditor = 0.03f;

        public EditorGUICanvas RootCanvas { get; private set; }
        public EditorGUICanvas GraphCanvas { get; private set; }
        public EditorGUICanvas ScaledCanvas { get; private set; }
        public Vector2 GlobalMousePosition { get; private set; }
        protected float mMinScale = 0.1f;
        protected float mMaxScale = 5f;
        bool mInitOk;

        List<DelayTask> mPostTasks = new List<DelayTask>();
        string mSerializeKey;
        public float TickTime { get; private set; }
        double mGUITime; // guitime
        double mTickTime;

        protected virtual void UpdateStateInfo()
        {
            statInfo = string.Format("<color=#808080><b><size=20>[{0} : 1]</size></b></color>", ScaledCanvas.LocalScale.ToString("0.00"));
        }

        void OnEditorUpdate()
        {
            TickTime = EditorApplication.isPlayingOrWillChangePlaymode ? mUpdateDeltaTime : mUpdateDeltaTimeEditor;
            double time = EditorApplication.timeSinceStartup;
            float t = TickTime;
            if (time > mTickTime + t)
            {
                mTickTime = time;
                Repaint();
            }
        }

        protected virtual void InitCanvas()
        {
            RootCanvas = new EditorGUICanvas();
            RootCanvas.Pivot = new Vector2(0, 0);

            ScaledCanvas = new EditorGUICanvas();
            ScaledCanvas.SortOrder = -1;
            RootCanvas.AddElement(ScaledCanvas);

            GraphCanvas = new EditorGUICanvas();
            ScaledCanvas.AddElement(GraphCanvas);

            GraphCanvas.GridLineColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            GraphCanvas.ShowGridLine = true;
            GraphCanvas.GridSize = 100;
            GraphCanvas.LocalRect = new Rect(-10000, -10000, 20000, 20000);
        }


        public EditorCanvasWindow() : base()
        {
            InitCanvas();
        }

        public virtual bool IsInitOk() { return true; }

        protected virtual void OnEnable()
        {
            mInitOk = false;
            mGUITime = EditorApplication.timeSinceStartup;
            ReadData();
            UpdateStateInfo();
            EditorApplication.update += OnEditorUpdate;
        }

        protected virtual void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SaveData();
        }

        protected virtual string GetSerializeKey()
        {
            var dic = new DirectoryInfo(Application.dataPath);
            return dic.Parent.Name + GetType() + ".sav";
        }

        void ReadData()
        {
            if (string.IsNullOrEmpty(mSerializeKey))
                mSerializeKey = GetSerializeKey();
            string str = EditorPrefs.GetString(mSerializeKey);
            if (!string.IsNullOrEmpty(str))
            {
                var obj = JsonMapper.ToObject(str);// JsonConvert.DeserializeObject<JObject>(str);
                ScaledCanvas.LocalScale = (float)obj["scale"];
                Rect rect = GraphCanvas.LocalRect;
                rect.x = Mathf.Clamp((float)obj["x"], -100000, 100000);
                rect.y = Mathf.Clamp((float)obj["y"], -100000, 100000);
                GraphCanvas.LocalRect = rect;
                OnReadCustomData(obj);
            }
        }

        void SaveData()
        {
            if (string.IsNullOrEmpty(mSerializeKey))
                mSerializeKey = GetSerializeKey();
            JsonData obj = new JsonData();
            OnSaveCustomData(obj);
            obj["scale"] = ScaledCanvas.LocalScale;
            obj["x"] = GraphCanvas.LocalRect.x;
            obj["y"] = GraphCanvas.LocalRect.y;
            EditorPrefs.SetString(mSerializeKey, JsonMapper.ToJson(obj));
        }

        protected virtual void OnReadCustomData(JsonData data) { }
        protected virtual void OnSaveCustomData(JsonData data) { }

        protected virtual void OnCanvasStart() { }
        protected virtual void OnPreGUI() { }

        protected virtual void OnGUI()
        {
            var t = EditorApplication.timeSinceStartup;
            deltaTime = Mathf.Min((float)(t - mGUITime), TickTime * 2f);
            bool initOk = IsInitOk();
            if (!mInitOk && initOk)
            {
                OnCanvasStart();
            }
            mInitOk = initOk;
            if (initOk)
            {
                OnPreGUI();
                ProcessMouseDeltaPos();
                Input.imeCompositionMode = IMECompositionMode.On;

                GUI.skin.label.richText = true;

                Rect rect = EditorGUILayout.BeginHorizontal();
                OnTitleGUI();
                EditorGUILayout.EndHorizontal();

                if (rect.width > 1)
                {
                    cachedViewportRect = rect;
                }

                clipRect = new Rect(cachedViewportRect.xMin + 1, cachedViewportRect.yMax + 1, position.width - 2, position.height - cachedViewportRect.height - 2);
                GUI.Label(clipRect, "", "CurveEditorBackground");
                OnCanvasGUI();
                GUI.Label(clipRect, statInfo);

                ProcessEvent();
                ProcessFocusCenter();

                OnPostGUI();

                for (int i = 0; i < mPostTasks.Count; i++)
                {
                    mPostTasks[i].Act();
                }
                mPostTasks.Clear();
            }
        }

        protected virtual void OnTitleGUI()
        {
            GUILayout.Label(" ", "TE toolbar");
        }

        protected virtual void OnPostGUI() { }
        
        protected virtual void OnResized() { }

        protected virtual void OnPainted() { }

        protected virtual void OnCanvasGUI()
        {
            GUI.BeginClip(clipRect);
            GlobalMousePosition = Event.current.mousePosition;
            Rect r = new Rect(Vector2.zero, clipRect.size);
            RootCanvas.LocalRect = r;
            ScaledCanvas.LocalRect = r;
            RootCanvas.OnCalculateGlobalRect(true, r);
            OnResized();
            RootCanvas.OnGUI(r);
            OnPainted();
            GUI.EndClip();
        }

        // 计算鼠标移动量
        void ProcessMouseDeltaPos()
        {
            if (Event.current.type == EventType.MouseDown)
            {
                mousePos = Event.current.mousePosition;
                mouseDeltaPos = Vector2.zero;
            }
            else if (Event.current.type == EventType.MouseDrag)
            {
                Vector2 pos = Event.current.mousePosition;
                mouseDeltaPos = pos - mousePos;
                mousePos = pos;
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                mouseDeltaPos = Vector2.zero;
            }
        }

        protected virtual Vector2 GetFocusDeltaPosition()
        {
            return ScaledCanvas.GlobalCentroid - GraphCanvas.GlobalCentroid;
        }

        //聚焦状态图中心
        private void ProcessFocusCenter()
        {
            if (focusCenter)
            {
                Vector2 delta = GetFocusDeltaPosition();
                if (delta.sqrMagnitude > 1)
                {
                    Rect rect = GraphCanvas.LocalRect;
                    rect.position += Vector2.Lerp(Vector2.zero, delta / GraphCanvas.GlobalScale, 0.1f);
                    GraphCanvas.LocalRect = rect;
                }
                else
                {
                    focusCenter = false;
                }
            }
        }

        void OnDragBegin()
        {
            RootCanvas.InteractDragBegin(mouseButton, GlobalMousePosition);
        }

        void OnDrag()
        {
            if (RootCanvas.InteractDrag(mouseButton, GlobalMousePosition, mouseDeltaPos))
                return;
            if (mouseButton == EMouseButton.middle || mouseButton == EMouseButton.right)
            {
                Rect rect = GraphCanvas.LocalRect;
                rect.position += mouseDeltaPos / (GraphCanvas.GlobalScale > 0 ? GraphCanvas.GlobalScale : 1);
                GraphCanvas.LocalRect = rect;
            }
        }

        void OnDragEnd()
        {
            RootCanvas.InteractDragEnd(mouseButton, GlobalMousePosition);
        }

        void OnClick()
        {
            RootCanvas.InteractMouseClick(mouseButton, GlobalMousePosition);
        }

        void OnKeyDown()
        {
            if (RootCanvas.InteractKeyDown(Event.current.keyCode))
            {
                Event.current.Use();
            }
        }

        void OnKeyUp()
        {
            if (RootCanvas.InteractKeyUp(Event.current.keyCode))
            {
                Event.current.Use();
                return;
            }
            if (Event.current.control && Event.current.keyCode == KeyCode.F)
            {
                focusCenter = true;
                Event.current.Use();
            }
        }
        
        protected virtual bool EnableDropAssets() { return false; }

        protected virtual void OnAcceptDrop() { }

        // 响应鼠标事件
        void ProcessEvent()
        {
            InterceptMouse = clipRect.Contains(Event.current.mousePosition);
            if (InterceptMouse && focusedWindow == this)
                DragAndDrop.visualMode = EnableDropAssets() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.None;
            if (!InterceptMouse)
            {
                return;
            }
            if (Event.current.type == EventType.KeyDown)
            {
                OnKeyDown();
            }
            else if (Event.current.type == EventType.KeyUp)
            {
                OnKeyUp();
            }
            if (Event.current.type == EventType.MouseDrag)
            {
                if (mouseAction == EMouseAction.none)
                {
                    mouseButton = (EMouseButton)Event.current.button;
                    mouseAction = EMouseAction.drag;
                    OnDragBegin();
                }
                if (mouseAction == EMouseAction.drag)
                {
                    OnDrag();
                }
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                if (mouseAction == EMouseAction.none)
                {
                    mouseButton = (EMouseButton)Event.current.button;
                    mouseAction = EMouseAction.click;
                    OnClick();
                }
                else if (mouseAction == EMouseAction.drag)
                {
                    OnDragEnd();
                }
                mouseAction = EMouseAction.none;
            }

            if (Event.current.type == EventType.ScrollWheel)
            {
                Vector2 cen = Vector2.zero;
                if (!Event.current.control)
                {
                    cen = GraphCanvas.Parent.CalculateLocalPosition(GlobalMousePosition);
                }
                float f = Mathf.Clamp(ScaledCanvas.LocalScale - Event.current.delta.y * ScaledCanvas.LocalScale * 0.05f, mMinScale, mMaxScale);
                if ((f < 1 && ScaledCanvas.LocalScale > 1) || (f > 1 && ScaledCanvas.LocalScale < 1))
                    f = 1;
                ScaledCanvas.LocalScale = f;
                if (!Event.current.control)
                {
                    Vector2 p = GraphCanvas.Parent.CalculateLocalPosition(GlobalMousePosition);
                    Vector2 delta = p - cen;
                    Rect r = GraphCanvas.LocalRect;
                    r.position += delta;
                    GraphCanvas.LocalRect = r;
                }
                UpdateStateInfo();
            }

            if (DragAndDrop.visualMode != DragAndDropVisualMode.None && Event.current.type == EventType.DragPerform)
            {
                OnAcceptDrop();
            }
        }

        public void AddDelayTask(int id, System.Action act)
        {
            if (act == null)
                return;
            for (int i = 0; i < mPostTasks.Count; i++)
            {
                if (mPostTasks[i].Id == id)
                {
                    mPostTasks[i].Act = act;
                    return;
                }
            }
            mPostTasks.Add(new DelayTask(id, act));
        }

        public bool IsDelayTaskScheduled(int id)
        {
            for (int i = 0; i < mPostTasks.Count; i++)
            {
                if (mPostTasks[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}