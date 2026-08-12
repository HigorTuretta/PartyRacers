using System.Collections.Generic;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Frontend.Party;
using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>Apresenta o MatchmakingService existente em uma grade fixa de 16 vagas.</summary>
    public sealed class MatchmakingController : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset blipTemplate;
        [SerializeField] FrontendRouter router;
        [SerializeField] PartyController partyController;
        [SerializeField] FrontendFlow flow;

        const int SlotCount = 16;
        const int DialBlipCount = 6;

        static readonly string[] StageLabels =
            { "PRONTOS", "PROCURANDO", "ENCONTRADOS", "PREENCHENDO", "CARREGANDO" };
        static readonly float[] BlipX = { 18f, 31f, 44f, 58f, 71f, 84f };
        static readonly float[] BlipY = { 62f, 38f, 70f, 44f, 66f, 40f };
        static readonly float[] SignalDurations = { 0.70f, 0.83f, 0.96f, 1.09f, 1.22f, 1.35f, 1.48f };

        sealed class PopTween
        {
            public VisualElement Element;
            public float Elapsed;
            public float Duration;
        }

        readonly List<TemplateContainer> blipHosts = new List<TemplateContainer>();
        readonly List<VisualElement> blipMarkers = new List<VisualElement>();
        readonly List<VisualElement> signalBars = new List<VisualElement>();
        readonly List<VisualElement> stagePulses = new List<VisualElement>();
        readonly List<PopTween> popTweens = new List<PopTween>();
        readonly string[] slotKeys = new string[SlotCount];

        VisualElement root;
        VisualElement blips;
        VisualElement needleScan;
        VisualElement needleLock;
        VisualElement signal;
        VisualElement progress;
        Label title;
        Label subtitle;
        Label timer;
        Label hint;
        Label roomCount;
        Label roomBots;
        MatchmakingService service;
        bool subscribed;
        float scanClock;
        MatchmakingStage displayedStage;
        bool hasDisplayedStage;

        public void Bind(VisualElement value)
        {
            Unbind();
            root = value;
            if (partyController == null)
                partyController = FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            if (flow == null)
                flow = FindAnyObjectByType<FrontendFlow>(FindObjectsInactive.Include);

            service = partyController != null ? partyController.Matchmaking : null;
            blips = root.Q<VisualElement>("Blips");
            needleScan = root.Q<VisualElement>("Needle_Scan");
            needleLock = root.Q<VisualElement>("Needle_Lock");
            signal = root.Q<VisualElement>("Signal");
            progress = root.Q<VisualElement>("Progress_Fill");
            title = root.Q<Label>("Title");
            subtitle = root.Q<Label>("Subtitle");
            timer = root.Q<Label>("Timer");
            hint = root.Q<Label>("Hint");
            roomCount = root.Q<Label>("Room_Count");
            roomBots = root.Q<Label>("Room_Bots");

            blipHosts.Clear();
            blipMarkers.Clear();
            signalBars.Clear();
            stagePulses.Clear();
            popTweens.Clear();
            System.Array.Clear(slotKeys, 0, slotKeys.Length);
            for (int i = 0; i < SignalDurations.Length; i++)
                signalBars.Add(root.Q<VisualElement>("Signal_" + i));
            stagePulses.AddRange(root.Query<VisualElement>("Now_Pulse").ToList());
            scanClock = 0f;
            hasDisplayedStage = false;

            root.Q<Button>("Btn_Cancel_Face").clicked += Cancel;
            root.Q<Button>("Btn_Race_Face").clicked += EnterRace;

            if (service != null)
            {
                service.StageChanged += OnStageChanged;
                service.SlotAdded += OnSlotAdded;
                subscribed = true;

                if (!service.Running)
                    partyController.IniciarBusca();
            }

            RefreshAll();
        }

        public void Unbind()
        {
            if (subscribed && service != null)
            {
                service.StageChanged -= OnStageChanged;
                service.SlotAdded -= OnSlotAdded;
            }

            subscribed = false;
            ResetPopTweens();
            blipHosts.Clear();
            blipMarkers.Clear();
            signalBars.Clear();
            stagePulses.Clear();
            root = null;
            service = null;
        }

        void Update()
        {
            if (root == null)
                return;

            if (service != null)
            {
                timer.text = Mathf.FloorToInt(service.ElapsedSearch).ToString("00") + "s";
                progress.style.width = Length.Percent(service.Search01 * 100f);

                UiStates.SetVariant(timer,
                    service.Stage >= MatchmakingStage.FillingWithBots ? "mm__timer--danger"
                    : service.Stage == MatchmakingStage.PlayersFound ? "mm__timer--amber" : "mm__timer--normal",
                    "mm__timer--normal", "mm__timer--amber", "mm__timer--danger");

                if (IsScanning(service.Stage))
                {
                    scanClock += Time.unscaledDeltaTime;
                    // CSS: 2.6 s para a ida e 2.6 s para a volta (alternate).
                    float wave = Mathf.PingPong(scanClock / 2.6f, 1f);
                    float eased = 0.5f - Mathf.Cos(wave * Mathf.PI) * 0.5f;
                    needleScan.style.left = Length.Percent(Mathf.Lerp(2f, 98f, eased));
                }
            }

            AnimateSignalBars();
            AnimateStagePulse();
            AnimatePopTweens();
        }

        void Cancel()
        {
            partyController?.CancelarBusca();
            router.Go(ScreenId.Lobby);
        }

        void EnterRace()
        {
            if (service == null || string.IsNullOrWhiteSpace(service.MapaSorteado))
                return;

            string scene = service.MapaSorteado;
            partyController?.CancelarBusca();
            flow?.CorrerEm(scene);
        }

        void OnStageChanged(MatchmakingStage _) => RefreshAll();

        void OnSlotAdded(MatchSlot _)
        {
            RefreshSlots();
            RefreshBlips();
            RefreshRoom();
        }

        void RefreshAll()
        {
            if (root == null)
                return;

            SetStage(service != null ? StageIndex(service.Stage) : 0);
            RefreshSlots();
            RefreshBlips();
            RefreshRoom();
            RefreshCopy();
        }

        void RefreshCopy()
        {
            MatchmakingStage stage = service != null ? service.Stage : MatchmakingStage.WaitingParty;
            int count = service?.Slots.Count ?? 0;
            int total = service != null ? service.JogadoresPorSala : SlotCount;
            switch (stage)
            {
                case MatchmakingStage.Searching:
                    title.text = "SINTONIZANDO CANAL";
                    subtitle.text = "Varrendo a frequência atrás de outros pilotos na fila.";
                    hint.text = "Encontrando pilotos com ping compatível...";
                    break;
                case MatchmakingStage.PlayersFound:
                    title.text = "PILOTOS NA FREQUÊNCIA";
                    subtitle.text = "Sinal travado. Continuando a busca enquanto houver vagas.";
                    hint.text = count + " pilotos captados \u00B7 faltam " + Mathf.Max(0, total - count) + " vagas";
                    break;
                case MatchmakingStage.FillingWithBots:
                    title.text = "PREENCHENDO COM BOTS";
                    subtitle.text = "Busca encerrada. Completando a grid para largar agora.";
                    hint.text = "Bots entram no lugar das vagas não preenchidas.";
                    break;
                case MatchmakingStage.MatchFound:
                    title.text = "PARTIDA ENCONTRADA";
                    subtitle.text = string.IsNullOrEmpty(service?.MapaSorteado)
                        ? "Sala pronta. Sorteando a pista."
                        : "Mapa sorteado: " + service.MapaSorteado.ToUpperInvariant() + " \u00B7 3 voltas.";
                    hint.text = "Carregando pista e sincronizando jogadores...";
                    break;
                case MatchmakingStage.LoadingMap:
                    title.text = "CARREGANDO PISTA";
                    subtitle.text = "Todos os pilotos estão a caminho do grid.";
                    hint.text = "Preparando a largada...";
                    break;
                default:
                    title.text = "AGUARDANDO O GRUPO";
                    subtitle.text = "Todos precisam marcar pronto antes de sintonizar o canal.";
                    hint.text = "A busca só começa com o grupo inteiro pronto.";
                    break;
            }

            bool found = stage == MatchmakingStage.MatchFound;
            UiStates.Show(root.Q<VisualElement>("Btn_Cancel"), !found && stage != MatchmakingStage.LoadingMap);
            UiStates.Show(root.Q<VisualElement>("Btn_Race"), found);
        }

        void SetStage(int stage)
        {
            MatchmakingStage current = service != null ? service.Stage : MatchmakingStage.WaitingParty;
            if (!hasDisplayedStage || IsScanning(current) && !IsScanning(displayedStage))
                scanClock = 0f;
            displayedStage = current;
            hasDisplayedStage = true;

            for (int i = 0; i < StageLabels.Length; i++)
            {
                var chip = root.Q<VisualElement>("Stage_" + i);
                string state = i < stage ? "State_Done" : i == stage ? "State_Now" : "State_Todo";
                UiStates.ShowOnly(chip, state, "State_Done", "State_Now", "State_Todo");
                chip.Q<Label>("Done_Label").text = StageLabels[i];
                chip.Q<Label>("Now_Label").text = StageLabels[i];
                chip.Q<Label>("Todo_Label").text = StageLabels[i];
            }

            UiStates.Show(needleScan, stage == 1 || stage == 2);
            UiStates.Show(needleLock, stage >= 3);
            UiStates.SetVariant(signal, stage == 1 || stage == 2 ? "mm__signal--scan" : "mm__signal--idle",
                "mm__signal--scan", "mm__signal--idle");
            RefreshCopy();
        }

        void RefreshSlots()
        {
            IReadOnlyList<MatchSlot> slots = service?.Slots;
            int count = slots?.Count ?? 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (i >= count)
                {
                    SetSlot(i, "State_Empty", default, "empty");
                    continue;
                }

                MatchSlot slot = slots[i];
                string state = slot.Bot ? "State_Bot" : slot.DoMeuGrupo ? "State_Mate" : "State_Human";
                SetSlot(i, state, slot, state + "|" + slot.Nome);
            }
        }

        void SetSlot(int index, string state, MatchSlot data, string key)
        {
            var slot = root.Q<VisualElement>("Slot_" + index);
            UiStates.ShowOnly(slot, state, "State_Human", "State_Mate", "State_Bot", "State_Empty");
            if (state == "State_Human")
            {
                slot.Q<Label>("Human_Name").text = data.Nome.ToUpperInvariant();
                UiStates.SetAvatarTint(slot.Q<VisualElement>("Human_Avatar"), data.Nome);
            }
            else if (state == "State_Mate")
            {
                slot.Q<Label>("Mate_Name").text = data.Nome.ToUpperInvariant();
                UiStates.SetAvatarTint(slot.Q<VisualElement>("Mate_Avatar"), data.Nome);
            }

            if (slotKeys[index] != key && (state == "State_Human" || state == "State_Bot"))
                StartPop(slot.Q<VisualElement>(state), 0.35f);
            slotKeys[index] = key;
        }

        void RefreshRoom()
        {
            int count = service?.Slots.Count ?? 0;
            int total = service != null ? service.JogadoresPorSala : SlotCount;
            int bots = service?.Bots ?? 0;
            roomCount.text = $"{count} / {total}";
            roomBots.text = bots == 1 ? "1 BOT" : $"{bots} BOTS";
            UiStates.Show(roomBots, bots > 0);
        }

        void RefreshBlips()
        {
            if (blips == null || blipTemplate == null)
                return;

            IReadOnlyList<MatchSlot> slots = service?.Slots;
            var humans = new List<MatchSlot>(DialBlipCount);
            if (slots != null)
            {
                for (int i = 0; i < slots.Count && humans.Count < DialBlipCount; i++)
                    if (!slots[i].Bot)
                        humans.Add(slots[i]);
            }

            while (blipHosts.Count > humans.Count)
            {
                int last = blipHosts.Count - 1;
                blipHosts[last].RemoveFromHierarchy();
                blipHosts.RemoveAt(last);
                blipMarkers.RemoveAt(last);
            }

            while (blipHosts.Count < humans.Count)
            {
                TemplateContainer host = blipTemplate.Instantiate();
                host.AddToClassList("blip-host");
                host.pickingMode = PickingMode.Ignore;
                VisualElement marker = host.Q<VisualElement>("Blip");
                blips.Add(host);
                blipHosts.Add(host);
                blipMarkers.Add(marker);
                StartPop(marker, 0.5f);
            }

            for (int i = 0; i < humans.Count; i++)
            {
                VisualElement marker = blipMarkers[i];
                marker.Q<Label>("Name").text = humans[i].Nome.ToUpperInvariant();
                marker.style.left = Length.Percent(BlipX[i]);
                marker.style.top = Length.Percent(BlipY[i]);
                UiStates.SetVariant(marker, i % 2 == 0 ? "blip--green" : "blip--sky",
                    "blip--green", "blip--sky", "blip--violet");
            }
        }

        void AnimateSignalBars()
        {
            for (int i = 0; i < signalBars.Count; i++)
            {
                float phase = Mathf.Repeat(Time.unscaledTime / SignalDurations[i], 1f);
                float wave = 0.5f - Mathf.Cos(phase * Mathf.PI * 2f) * 0.5f;
                signalBars[i].style.opacity = Mathf.Lerp(0.35f, 1f, wave);
            }
        }

        void AnimateStagePulse()
        {
            float phase = Mathf.Repeat(Time.unscaledTime, 1f);
            float wave = 0.5f - Mathf.Cos(phase * Mathf.PI * 2f) * 0.5f;
            float opacity = Mathf.Lerp(0.35f, 1f, wave);
            for (int i = 0; i < stagePulses.Count; i++)
                stagePulses[i].style.opacity = opacity;
        }

        void StartPop(VisualElement element, float duration)
        {
            if (element == null)
                return;

            for (int i = popTweens.Count - 1; i >= 0; i--)
                if (popTweens[i].Element == element)
                    popTweens.RemoveAt(i);

            element.style.opacity = 0f;
            element.style.scale = new Scale(new Vector3(0.4f, 0.4f, 1f));
            popTweens.Add(new PopTween { Element = element, Duration = duration });
        }

        void AnimatePopTweens()
        {
            for (int i = popTweens.Count - 1; i >= 0; i--)
            {
                PopTween tween = popTweens[i];
                if (tween.Element == null || tween.Element.panel == null)
                {
                    popTweens.RemoveAt(i);
                    continue;
                }

                tween.Elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(tween.Elapsed / tween.Duration);
                float scale;
                float opacity;
                if (t < 0.4f)
                {
                    float eased = EaseOutCubic(t / 0.4f);
                    scale = Mathf.Lerp(0.4f, 1.15f, eased);
                    opacity = eased;
                }
                else
                {
                    float eased = EaseOutCubic((t - 0.4f) / 0.6f);
                    scale = Mathf.Lerp(1.15f, 1f, eased);
                    opacity = 1f;
                }

                tween.Element.style.opacity = opacity;
                tween.Element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                if (t < 1f)
                    continue;

                tween.Element.style.opacity = StyleKeyword.Null;
                tween.Element.style.scale = StyleKeyword.Null;
                popTweens.RemoveAt(i);
            }
        }

        void ResetPopTweens()
        {
            for (int i = 0; i < popTweens.Count; i++)
            {
                VisualElement element = popTweens[i].Element;
                if (element == null) continue;
                element.style.opacity = StyleKeyword.Null;
                element.style.scale = StyleKeyword.Null;
            }
            popTweens.Clear();
        }

        static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        static bool IsScanning(MatchmakingStage stage)
            => stage == MatchmakingStage.Searching || stage == MatchmakingStage.PlayersFound;

        static int StageIndex(MatchmakingStage stage) => stage switch
        {
            MatchmakingStage.WaitingParty => 0,
            MatchmakingStage.Searching => 1,
            MatchmakingStage.PlayersFound => 2,
            MatchmakingStage.FillingWithBots => 3,
            _ => 4,
        };
    }
}
