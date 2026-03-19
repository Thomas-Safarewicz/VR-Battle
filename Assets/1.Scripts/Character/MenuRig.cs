using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace _1.Scripts.Character
{
    public class MenuRig : MonoBehaviour
    {
        public static MenuRig Instance { get; private set; }

        [Header("State")]
        public bool InMenu;

        [Header("GameObjects")]
        public List<GameObject> enableInMenu = new();

        public List<GameObject> disableInMenu = new();

        [Header("Components")]
        public List<Behaviour> enableComponentsInMenu = new();

        public List<Behaviour> disableComponentsInMenu = new();

        [Header("Events")]
        public UnityEvent onMenuEnter;

        public UnityEvent onMenuExit;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (InMenu)
            {
                InMenu = false;
                EnterMenu();
            }
            else
            {
                InMenu = true;
                ExitMenu();
            }
        }

        public void EnterMenu()
        {
            if (InMenu) return;

            InMenu = true;

            ToggleObjects(true);
            ToggleComponents(true);

            onMenuEnter?.Invoke();
        }

        public void ExitMenu()
        {
            if (!InMenu) return;

            InMenu = false;

            ToggleObjects(false);
            ToggleComponents(false);

            onMenuExit?.Invoke();
        }

        public void ToggleMenu()
        {
            if (InMenu)
                ExitMenu();
            else
                EnterMenu();
        }

        private void ToggleObjects(bool menuState)
        {
            foreach (GameObject obj in enableInMenu)
            {
                if (obj)
                {
                    obj.SetActive(menuState);
                }
            }

            foreach (GameObject obj in disableInMenu)
            {
                if (obj)
                {
                    obj.SetActive(!menuState);
                }
            }
        }

        void ToggleComponents(bool menuState)
        {
            foreach (Behaviour comp in enableComponentsInMenu)
            {
                if (comp)
                {
                    comp.enabled = menuState;
                }
            }

            foreach (Behaviour comp in disableComponentsInMenu)
            {
                if (comp)
                {
                    comp.enabled = !menuState;
                }
            }
        }
    }
}