using UnityEngine;
using Yarn.Unity;

public class SubtitleController : MonoBehaviour
{
    private DialogueRunner runner;
    private LinePresenter presenter;

    private void Awake()
    {
        runner = FindAnyObjectByType<DialogueRunner>();
        if (runner == null)
            Debug.LogError("No DialogueRunner found in the scene!");

        presenter = FindAnyObjectByType<LinePresenter>();

        if (presenter == null)
            Debug.LogError("No LinePresenter found in the scene!");
    }

    private void Start()
    {
        AudioClipManager.Instance.dialogueHasStarted.AddListener(test);
    }

    private void test(AudioClipSO clip)
    {
        string yarnNode = clip.GetYarnNode();
        if (yarnNode == null) return;

        if (runner) runner.StartDialogue(yarnNode);
        float audioLength = clip.GetAudioClip().length;

        if (runner.VariableStorage.TryGetValue<float>("$wordCount", out var wordCount))
        {

            int wordPerSecond = Mathf.FloorToInt(wordCount / audioLength);
            presenter.wordsPerSecond = wordPerSecond;
        }
    }
}
