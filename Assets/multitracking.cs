using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class multitracking : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] buttons;
    public Renderer[] dirts;
    public Renderer[] prints;
    public Texture[] dirtTextures;
    public Texture[] printTextures;
    public GameObject statusLight;
    public GameObject[] covers;
    public Mesh hlMesh;

    private int[] displayedPrints;
    private int solutionPrint;
    private int selectedSubmissionPrint;
    private int stage;
    private List<int> invalidTimes = new List<int>();

    private static readonly string[] animalNames = new string[36] { "badger", "bear", "beaver", "cat", "cow", "crow", "dog", "duck", "eagle", "ferret", "fox", "frog", "gerbil", "goat", "hedgehog", "heron", "horse", "lynx", "marten", "moose", "mouse", "otter", "owl", "partridge", "pig", "pigeon", "rabbit", "raccoon", "rat", "sparrow", "red deer", "roe deer", "sheep", "squirrel", "weasel", "wolf" };
    private bool TwitchPlaysActive;
    private bool activated;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in buttons)
            button.OnInteract += delegate () { PressButton(button); return false; };
        module.OnActivate += delegate () { activated = true; StartCoroutine(DisableStuff()); };
    }

    private void Start()
    {
        foreach (Renderer dirt in dirts)
        {
            dirt.material.mainTexture = dirtTextures.PickRandom();
            dirt.material.mainTextureOffset = new Vector2(rnd.Range(0f, 1f), rnd.Range(0f, 1f));
        }
        var listA = GetRectangle();
        tryAgain:
        var listB = GetRectangle();
        if (listB.Any(x => listA.Contains(x)))
            goto tryAgain;
        var allPrints = listA.Concat(listB).ToList();
        allPrints.Shuffle();
        displayedPrints = allPrints.Take(7).ToArray();
        solutionPrint = allPrints.Last();
        for (int i = 0; i < 7; i++)
        {
            prints[i].material.mainTexture = printTextures[displayedPrints[i]];
            prints[i].transform.localEulerAngles = new Vector3(90f, rnd.Range(0f, 360f), 0f);
        }
        selectedSubmissionPrint = rnd.Range(0, 36);
        prints[7].material.mainTexture = printTextures[selectedSubmissionPrint];
        GenerateStage();
    }

    private void GenerateStage()
    {
        var thisPrint = displayedPrints[stage];
        var thisName = animalNames[thisPrint];
        Debug.LogFormat("[Multitracking #{0}] Stage {1}:", moduleId, stage + 1);
        Debug.LogFormat("[Multitracking #{0}] The displayed track comes from a{2} {1}.", moduleId, thisName, "aeiou".Contains(thisName.First()) ? "n" : "");
        var N = ((bomb.GetSerialNumberNumbers().First() * ((thisPrint / 6) + 1)) + (bomb.GetSerialNumberNumbers().Last() * ((thisPrint % 6) + 1)) + thisName.Where(x => !char.IsWhiteSpace(x)).Count()) % 10;
        while (invalidTimes.Contains(N))
            N = (N + 1) % 10;
        invalidTimes.Add(N);
        Debug.LogFormat("[Multitracking #{0}] New set of invalid timer digits: [{1}]", moduleId, invalidTimes.Join(", "));
    }

    private void PressButton(KMSelectable button)
    {
        button.AddInteractionPunch(.25f);
        var ix = Array.IndexOf(buttons, button);
        if (moduleSolved || ix != stage || !activated)
            return;
        var submittedTime = ((int)bomb.GetTime()) % 10;
        if (stage != 7)
            Debug.LogFormat("[Multitracking #{0}] Submitted on a {1}.", moduleId, submittedTime);
        if (ix != 7)
        {
            if (invalidTimes.Contains(submittedTime))
            {
                Debug.LogFormat("[Multitracking #{0}] That was an invalid digit. Strike!", moduleId);
                module.HandleStrike();
            }
            else
            {
                Debug.LogFormat("[Multitracking #{0}] That was a valid digit. Progressing...", moduleId);
                covers[stage].SetActive(false);
                audio.PlaySoundAtTransform("break" + rnd.Range(1, 4), covers[stage].transform);
                stage++;
                var hclone = buttons[stage].Highlight.transform.Find("Highlight(Clone)");
                if (hclone != null)
                    hclone.GetComponent<MeshFilter>().mesh = hlMesh;
                if (stage != 7)
                    GenerateStage();
                else
                    Debug.LogFormat("[Multitracking #{0}] Time for submission. The animal that ate the food was a{2} {1}.", moduleId, animalNames[solutionPrint], "aeiou".Contains(animalNames[solutionPrint].First()) ? "n" : "");
            }
        }
        else
        {
            if (invalidTimes.Contains(submittedTime))
            {
                var x = selectedSubmissionPrint % 6;
                var y = selectedSubmissionPrint / 6;
                if (submittedTime > 4)
                {
                    x = (x + 1) % 6;
                    selectedSubmissionPrint = y * 6 + x;
                }
                else
                {
                    y = (y + 1) % 6;
                    selectedSubmissionPrint = y * 6 + x;
                }
                audio.PlaySoundAtTransform("dig" + rnd.Range(1, 6), covers[7].transform);
                prints[7].material.mainTexture = printTextures[selectedSubmissionPrint];
                prints[7].transform.localEulerAngles = new Vector3(90f, rnd.Range(0f, 360f), 0f);
            }
            else
            {
                Debug.LogFormat("[Multitracking #{0}] You submitted {1}.", moduleId, animalNames[selectedSubmissionPrint]);
                if (selectedSubmissionPrint == solutionPrint)
                {
                    moduleSolved = true;
                    module.HandlePass();
                    audio.PlaySoundAtTransform("solve", transform);
                    Debug.LogFormat("[Multitracking #{0}] That was correct. Module solved, you cracked the case!", moduleId);
                    statusLight.SetActive(true);
                    covers[7].SetActive(false);
                    for (int i = 0; i < 8; i++)
                    {
                        var hclone = buttons[i].Highlight.transform.Find("Highlight(Clone)");
                        if (hclone != null)
                            hclone.GetComponent<MeshFilter>().mesh = null;
                    }
                }
                else
                {
                    Debug.LogFormat("[Multitracking #{0}] That was incorrect. Strike!", moduleId);
                    module.HandleStrike();
                }
            }
        }
    }

    private static List<int> GetRectangle()
    {
        var tablePlaces = new List<int> { rnd.Range(0, 36) };
        var horiz = rnd.Range(0, 5) + 1;
        var verti = rnd.Range(0, 5) + 1;
        tablePlaces.Add((tablePlaces[0] + (6 * verti) + 36) % 36);
        if ((tablePlaces[0] % 6) + horiz > 5)
        {
            tablePlaces.Add(tablePlaces[0] - (6 - horiz));
            tablePlaces.Add(tablePlaces[1] - (6 - horiz));
        }
        else
        {
            tablePlaces.Add(tablePlaces[0] + horiz);
            tablePlaces.Add(tablePlaces[1] + horiz);
        }
        tablePlaces.Sort();
        return tablePlaces;
    }

    private IEnumerator DisableStuff()
    {
        yield return null;
        if (!TwitchPlaysActive)
            statusLight.SetActive(false);
        covers[7].SetActive(!TwitchPlaysActive);
        for (int i = 1; i < 8; i++)
        {
            var hclone = buttons[i].Highlight.transform.Find("Highlight(Clone)");
            if (hclone != null)
                hclone.GetComponent<MeshFilter>().mesh = null;
        }
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} <#> [Presses the newest opened box when the last digit of the timer is #.]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.Trim();
        var digits = "0123456789".Select(x => x.ToString()).ToArray();
        if (!digits.Contains(input))
            yield break;
        yield return null;
        var ix = Array.IndexOf(digits, input);
        while (((int)bomb.GetTime()) % 10 != ix)
            yield return "trycancel";
        buttons[stage].OnInteract();
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!moduleSolved)
        {
            if (stage != 7)
            {
                while (invalidTimes.Contains(((int)bomb.GetTime()) % 10))
                {
                    yield return true;
                    yield return null;
                }
                yield return null;
                buttons[stage].OnInteract();
            }
            else
            {
                if (selectedSubmissionPrint % 6 != solutionPrint % 6)
                {
                    while (!invalidTimes.Contains((int)bomb.GetTime() % 10) || (int)bomb.GetTime() % 10 < 5)
                    {
                        yield return true;
                        yield return null;
                    }
                    yield return null;
                    buttons[stage].OnInteract();
                }
                if (selectedSubmissionPrint / 6 != solutionPrint / 6)
                {
                    while (!invalidTimes.Contains((int)bomb.GetTime() % 10) || (int)bomb.GetTime() % 10 > 4)
                    {
                        yield return true;
                        yield return null;
                    }
                    yield return null;
                    buttons[stage].OnInteract();
                }
                if (selectedSubmissionPrint == solutionPrint)
                {
                    while (invalidTimes.Contains((int)bomb.GetTime() % 10))
                    {
                        yield return true;
                        yield return null;
                    }
                    yield return null;
                    buttons[stage].OnInteract();
                }
            }
        }
    }
}
