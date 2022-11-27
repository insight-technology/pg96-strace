using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

/// https://stackoverflow.com/a/31141975
public static partial class JTokenExtensions
{
    public static bool IsNull(this JToken token)
    {
        return token == null || token.Type == JTokenType.Null;
    }
}

public class ContextManager : MonoBehaviour
{
    public static ContextManager instance;

    public GameObject canvas;

    private Slider frameScaleSlider;

    private Slider slider;

    [SerializeField] public int frame = 0;

    [SerializeField] public int frameScale = 30;

    public bool isPlaying;

    public List<JObject> events = new List<JObject>();

    public GameObject prefabProcess;
    public GameObject prefabEdge;

    private Dictionary<int, GameObject> processes = new Dictionary<int, GameObject>();

    public Dictionary<int, Vector3> pPosHistory = new Dictionary<int, Vector3>();

    private bool initializing;

    private int firstPid = 1;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        frameScaleSlider = canvas.transform.Find("FrameScaleSlider").GetComponent<Slider>();
        frameScaleSlider.onValueChanged.AddListener((value) => { frameScale = (int)value; });

        slider = canvas.transform.Find("Slider").GetComponent<Slider>();
        slider.onValueChanged.AddListener((value) =>
        {
            initializing = true;

            frame = (int)value;

            ClearObjects();
            RestoreTree();

            initializing = false;
        });

        isPlaying = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!initializing && isPlaying)
        {
            if (Time.frameCount % frameScale == 0)
            {
                Increment();
            }
        }
    }

    public void Increment()
    {
        if (frame < events.Count - 1)
        {
            frame++;

            var e = events[frame];

            ApplyStraceEvent(e);
            SyncUITextWithEvent(e);

            slider.SetValueWithoutNotify(frame);
        }
    }

    /// TODO: 差分適用
    public void Decrement()
    {
        if (frame > 0)
        {
            initializing = true;

            frame--;

            ClearObjects();
            RestoreTree();

            initializing = false;
        }
    }

    public void Jump(int newFrame)
    {
        if (newFrame < events.Count)
        {
            initializing = true;

            frame = newFrame;

            ClearObjects();
            RestoreTree();

            initializing = false;
        }
    }

    /// 決め打ち
    private string PidToName(int pid)
    {
        int diff = pid - firstPid;

        var name = "postgres";
        if (diff == 4)
        {
            name = "startup";
        }
        else if (diff == 5)
        {
            name = "checkpointer process";
        }
        else if (diff == 6)
        {
            name = "writer process";
        }
        else if (diff == 7)
        {
            name = "wal writer process";
        }
        else if (diff == 8)
        {
            name = "autovacuum launcher process";
        }
        else if (diff == 9)
        {
            name = "stats collector process";
        }
        return name;
    }

    private void AppendProcessNode(JToken p)
    {
        var pid = p["pid"].Value<Int32>();
        // var name = p["name"].ToString();  // XXX
        var name = PidToName(pid);

        var memory = Int32.Parse(p["memory"].ToString());

        GameObject o = Instantiate(prefabProcess, GetNewProcessCoord(pid), Quaternion.identity);
        Node n = o.GetComponent<Node>();
        n.text = name + " (" + pid + ")\n0";

        n.ManipMem(memory);

        n.pid = pid;

        var fdTree = p["fd_table"];
        foreach (var f in fdTree.Values())
        {
            n.AddFd(f);
        }

        processes.Add(pid, o);
    }

    private void ApplyStraceEvent(JObject e)
    {
        var evt = e["event"];
        var evtName = evt["name"].ToString();


        var pid = evt["pid"].Value<Int32>();
        GameObject o;
        switch (evtName)
        {
            case "add_proc":
                AppendProcessNode(e["p_table"][pid.ToString()]);
                break;
            case "close_proc":
                if (processes.TryGetValue(pid, out o))
                {
                    Destroy(o);
                    processes.Remove(pid);
                }
                break;
            case "open_fd":
            case "accept":
                if (processes.TryGetValue(pid, out o))
                {
                    var fd = evt["fd"].ToString();
                    var f = e["p_table"][pid.ToString()]["fd_table"][fd];

                    o.GetComponent<Node>().AddFd(f);
                }
                break;
            case "close_fd":
                if (processes.TryGetValue(pid, out o))
                {
                    o.GetComponent<Node>().RemoveFd(evt["fd"].Value<Int32>());
                }
                break;
            case "write_fd":
                if (processes.TryGetValue(pid, out o))
                {
                    o.GetComponent<Node>().UpdateFdCounter(evt["fd"].Value<Int32>(), 0, Int32.Parse(evt["len"].ToString()));
                }
                break;
            case "read_fd":
                if (processes.TryGetValue(pid, out o))
                {
                    o.GetComponent<Node>().UpdateFdCounter(evt["fd"].Value<Int32>(), Int32.Parse(evt["len"].ToString()), 0);
                }
                break;
            case "bind":
                if (processes.TryGetValue(pid, out o))
                {
                    var n = o.GetComponent<Node>();
                    n.UpdateSocket(evt["fd"].Value<Int32>(), evt["family"].ToString(), evt["bind"].ToString(), null, null);
                }
                break;
            case "listen":
                if (processes.TryGetValue(pid, out o))
                {
                    var n = o.GetComponent<Node>();
                    n.UpdateSocket(evt["fd"].Value<Int32>(), null, null, null, true);
                }
                break;
            case "connect":
                if (processes.TryGetValue(pid, out o))
                {
                    var n = o.GetComponent<Node>();
                    n.UpdateSocket(evt["fd"].Value<Int32>(), evt["family"].ToString(), null, evt["target"].ToString(), true);
                }
                break;
            case "manip_mem":
                if (processes.TryGetValue(pid, out o))
                {
                    var n = o.GetComponent<Node>();
                    n.ManipMem(Int32.Parse(evt["amount"].ToString()));
                }
                break;
            default:
                break;
        }
    }

    private void SyncUITextWithEvent(JToken e)
    {
        var timePart = e["time"].ToString();
        canvas.transform.Find("TopLeftText").GetComponent<TMPro.TextMeshProUGUI>().text = "(" + frame + ") " + timePart;
        canvas.transform.Find("CurCmdText").GetComponent<TMPro.TextMeshProUGUI>().text = e["event"].ToString(Newtonsoft.Json.Formatting.None);
    }

    private void RestoreTree()
    {
        if (frame >= events.Count)
        {
            Debug.Log("frame out of events count. " + frame + " " + events.Count);
            return;
        }

        // 直近のp_tableを持つイベントレコードまで遡る
        // 0番目のp_tableがnullは考えてない
        int keyFrame = frame;
        for (; keyFrame >= 0; keyFrame--)
        {
            if (!events[keyFrame]["p_table"].IsNull())
            {
                break;
            }
        }

        var e = events[keyFrame];
        var pTree = e["p_table"];

        foreach (var p in pTree.Values())
        {
            AppendProcessNode(p);
        }

        // 差分適用
        for (int i = keyFrame + 1; i <= frame; i++)
        {
            ApplyStraceEvent(events[i]);
        }

        SyncUITextWithEvent(events[frame]);
    }

    public void LoadNewFile(string contents)
    {
        initializing = true;

        ClearWorld();

        events = contents.Split('\n')
            .Select(elm => { try { return JObject.Parse(elm); } catch { return null; } })
            .Where(elm => elm != null)
            .ToList<JObject>();

        if (events.Count > 0)
        {
            slider.maxValue = events.Count;
            firstPid = events[0]["event"]["pid"].Value<Int32>();
        }
        else
        {
            slider.maxValue = 0;
        }

        RestoreTree();

        initializing = false;
    }

    /// できるだけ前と同じ場所に出現させるため
    private Vector3 GetNewProcessCoord(int pid)
    {
        if (pPosHistory.ContainsKey(pid))
        {
            return pPosHistory[pid];
        }
        else
        {
            var v = new Vector3(10.0f * UnityEngine.Random.Range(-1f, 1f), 10.0f, 10.0f * UnityEngine.Random.Range(-1f, 1f));
            pPosHistory[pid] = v;
            return v;
        }
    }

    private void ClearObjects()
    {
        foreach (var o in processes.Values)
        {
            Destroy(o);
        }
        processes.Clear();
    }

    private void ClearWorld()
    {
        isPlaying = false;

        frame = 0;
        ClearObjects();

        events.Clear();

        slider.SetValueWithoutNotify(0);
    }
}
