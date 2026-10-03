using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A bonus pickup placed off the main path (behind hurdles): walking into it adds Notes to the save once
    /// per run. The model spins and bobs on the beat.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NoteCache : MonoBehaviour
    {
        [SerializeField] int notes = 75;
        [SerializeField] Transform model;
        [SerializeField] CombatFeedback feedback;

        bool _taken;

        public event System.Action<int> Collected;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (feedback == null) feedback = FindAnyObjectByType<CombatFeedback>();
        }

        void Update()
        {
            if (model == null) return;
            model.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            model.localPosition = new Vector3(0f, 0.6f + Mathf.Sin(Time.time * 3f) * 0.12f, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_taken || other.GetComponentInParent<PlayerMotor>() == null) return;
            _taken = true;
            new ProgressionRules(SaveSystem.Current).AddNotes(notes);
            SaveSystem.Save();
            if (feedback != null) feedback.PlayParry();
            Collected?.Invoke(notes);
            gameObject.SetActive(false);
        }
    }
}
