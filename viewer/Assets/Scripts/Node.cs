using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

class Util
{
    public static string toSizeText(int v)
    {
        if (v < 1024)
        {
            return v.ToString();
        }
        else
        {
            return (v / 1024.0f).ToString("F2") + "K";
        }
    }
}

class FDFileOpt
{
    public string target;
}

class FDSocketOpt
{
    public string domain = "";
    public string stype = "";
    public string protocol = "";
    public bool? isOut = null;
    public string family = null;
    public string bind = null;
    public string target = null;
}

class FDInfo
{
    public GameObject obj;

    public int vertPos;

    public string type;

    public int fd;
    // public string name;
    public int r;
    public int w;

    public FDFileOpt fileOpt = null;
    public FDSocketOpt socketOpt = null;

    public string GetString()
    {
        string rText = Util.toSizeText(r);
        string wText = Util.toSizeText(w);

        string desc = "";
        if (type == "SFile")
        {
            desc = fileOpt.target;
        }
        else if (type == "SSocket")
        {
            desc = socketOpt.domain;

            if (socketOpt.target != null || socketOpt.bind != null)
            {
                desc = socketOpt.family;

                if (socketOpt.target != null)
                {
                    desc += " " + socketOpt.target;
                }
                else
                {
                    desc += " " + socketOpt.bind;
                }

                if (socketOpt.isOut.HasValue)
                {
                    if (socketOpt.isOut.Value)
                    {
                        desc += " (out)";
                    }
                    else
                    {
                        desc += " (in)";
                    }
                }
            }
        }
        else if (type == "SStd")
        {
            desc = "std";
        }
        else if (type == "SEpoll")
        {
            desc = "epoll";
        }
        else if (type == "SPipe")
        {
            desc = "pipe";
        }

        return "(" + fd + ") " + desc + " r: " + rText + " w: " + wText;
    }
}

public class Node : MonoBehaviour
{
    [SerializeField] public Material fileMaterial;
    [SerializeField] public Material sockMaterial;
    [SerializeField] public Material epollMaterial;

    Stack<int> freePosList = new Stack<int>();


    public int pid;

    public int mem = 0;

    float diffPos = 0.5f;

    Dictionary<int, FDInfo> fds = new Dictionary<int, FDInfo>();

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public string text
    {
        set
        {
            transform.Find("Canvas").Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text = value;
        }
    }


    public void AddFd(JToken f)
    {
        var fd = Int32.Parse(f["fd"].ToString());

        var type = f["class"].ToString();

        var i = new FDInfo();
        i.type = type;
        i.fd = fd;
        i.r = Int32.Parse(f["r"].ToString());
        i.w = Int32.Parse(f["w"].ToString());

        if (type == "SFile")
        {
            i.fileOpt = new FDFileOpt
            {
                target = f["target"].ToString()
            };
        }
        else if (type == "SSocket")
        {
            i.socketOpt = new FDSocketOpt
            {
                domain = f["domain"].ToString(),
                stype = f["stype"].ToString(),
                protocol = f["protocol"].ToString(),

                isOut = f["isOut"].IsNull() ? null : (bool)f["isOut"],
                family = f["family"].IsNull() ? null : f["family"].ToString(),
                bind = f["bind"].IsNull() ? null : f["bind"].ToString(),
                target = f["target"].IsNull() ? null : f["target"].ToString()
            };
        }

        Vector3 templatePos = transform.Find("TextTemplate").transform.localPosition;

        if (freePosList.Count > 0)
        {
            i.vertPos = freePosList.Pop();
        }
        else
        {
            i.vertPos = fds.Values.Count + 1;
        }

        float pos = templatePos.y - i.vertPos * diffPos;

        var t = Instantiate(transform.Find("TextTemplate").gameObject, gameObject.transform);
        t.GetComponent<TMPro.TextMeshPro>().text = i.GetString();

        if (type == "SFile")
        {
            t.GetComponent<TMPro.TextMeshPro>().color = fileMaterial.color;
        }
        else if (type == "SSocket")
        {
            t.GetComponent<TMPro.TextMeshPro>().color = sockMaterial.color;
        }
        else if (type == "SEpoll")
        {
            t.GetComponent<TMPro.TextMeshPro>().color = epollMaterial.color;
        }
        else if (type == "SPipe")
        {
            t.GetComponent<TMPro.TextMeshPro>().color = epollMaterial.color;
        }

        t.transform.localPosition = new Vector3(templatePos.x, pos, templatePos.z);

        i.obj = t;

        fds[fd] = i;
    }

    public void RemoveFd(int fd)
    {
        if (!fds.ContainsKey(fd))
        {
            return;
        }

        Destroy(fds[fd].obj);

        freePosList.Push(fds[fd].vertPos);

        fds.Remove(fd);
    }

    public void UpdateFdCounter(int fd, int rDiff, int wDiff)
    {
        if (!fds.ContainsKey(fd))
        {
            return;
        }

        fds[fd].r += rDiff;
        fds[fd].w += wDiff;

        syncFdText(fd);
    }

    public void ManipMem(int amount)
    {
        mem += amount;

        text = transform.Find("Canvas").Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text.Split('\n')[0] + '\n' + Util.toSizeText(mem);
    }

    public void UpdateSocket(int fd, string family, string bind, string target, bool? isOut)
    {
        if (!fds.ContainsKey(fd))
        {
            return;
        }

        if (fds[fd].type != "SSocket")
        {
            return;
        }

        // not nullからnullへの変化はないとする

        if (family != null)
        {
            fds[fd].socketOpt.family = family;
        }
        if (bind != null)
        {
            fds[fd].socketOpt.bind = bind;
        }
        if (target != null)
        {
            fds[fd].socketOpt.target = target;
        }
        if (isOut.HasValue)
        {
            fds[fd].socketOpt.isOut = isOut;
        }

        syncFdText(fd);
    }

    void syncFdText(int fd)
    {
        // チェックなし

        fds[fd].obj.GetComponent<TMPro.TextMeshPro>().text = fds[fd].GetString();
    }


    void OnMouseDrag()
    {
        Vector3 objectPointInScreen
            = Camera.main.WorldToScreenPoint(this.transform.position);

        Vector3 mousePointInScreen
            = new Vector3(Input.mousePosition.x,
                          Input.mousePosition.y,
                          objectPointInScreen.z);

        Vector3 mousePointInWorld = Camera.main.ScreenToWorldPoint(mousePointInScreen);
        mousePointInWorld.z = this.transform.position.z;
        this.transform.position = mousePointInWorld;

        ContextManager.instance.pPosHistory[pid] = this.transform.position;
    }
}
