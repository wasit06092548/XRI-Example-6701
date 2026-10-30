using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

/// <summary>
/// TRIGGER TOGGLE LIGHT — สวิตช์ไฟด้วยปุ่มไก (Feature ใหม่ #2 ที่ต่างจาก workshop)
/// การทำงาน:
/// - จับวัตถุ (เช่น สวิตช์/รีโมท) ไว้แล้ว "กดไก 1 ครั้ง" = สลับเปิด/ปิดไฟ
/// - ทุกครั้งที่สลับจะมีเสียงคลิก (ถ้าใส่คลิปเสียงมา)
/// - นับเป็นการใช้ Activate Input ของ XRI อีกรูปแบบหนึ่ง (ต่างจากเป่าลูกโป่งที่ต้องกดค้าง)
///
/// วิธีติดตั้ง:
/// 1. สร้างวัตถุสวิตช์ (Cube เล็กๆ หรือใช้ model ในโปรเจกต์) วางในฉาก
/// 2. Add Component > XR Grab Interactable
/// 3. Add Component > Trigger Toggle Light (สคริปต์นี้)
/// 4. สร้างไฟที่ต้องการคุม: GameObject > Light > Point Light (หรือ Spot Light)
///    แล้วลาก Light ทั้งหมดใส่ช่อง Lights ใน Inspector
/// </summary>
public class TriggerToggleLight : MonoBehaviour
{
    [Header("ไฟที่คุม")]
    [Tooltip("ไฟทั้งหมดที่สวิตช์นี้คุม (ลาก Light ใส่ได้หลายดวง)")]
    [SerializeField] Light[] controlledLights;

    [Header("การกด")]
    [Tooltip("ค่าไกที่ถือว่า 'กด' (0-1)")]
    [SerializeField] float triggerThreshold = 0.6f;

    [Tooltip("เสียงคลิกตอนสลับ (ใส่หรือไม่ใส่ก็ได้)")]
    [SerializeField] AudioClip clickSound;

    [Tooltip("ความดังเสียงคลิก")]
    [SerializeField] float clickVolume = 0.8f;

    IXRSelectInteractable m_Interactable;
    XRInputButtonReader m_ActivateInput;
    bool m_IsHeld;
    bool m_IsOn;
    bool m_TriggerWasDown;

    void Awake()
    {
        // สถานะเริ่มต้น = ตามไฟดวงแรก
        if (controlledLights != null && controlledLights.Length > 0 && controlledLights[0] != null)
            m_IsOn = controlledLights[0].enabled;
    }

    void OnEnable()
    {
        m_Interactable = GetComponent<IXRSelectInteractable>();
        if (m_Interactable == null || m_Interactable as Object == null)
        {
            Debug.LogWarning($"[TriggerToggleLight] {name} ยังไม่มี XR Grab Interactable — ใส่ก่อนถึงจะสลับไฟได้", this);
            enabled = false;
            return;
        }

        m_Interactable.selectEntered.AddListener(OnGrabbed);
        m_Interactable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        if (m_Interactable as Object != null)
        {
            m_Interactable.selectEntered.RemoveListener(OnGrabbed);
            m_Interactable.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        m_IsHeld = true;
        m_TriggerWasDown = false;

        var controllerInteractor = args.interactorObject as XRBaseInputInteractor;
        m_ActivateInput = controllerInteractor != null ? controllerInteractor.activateInput : null;
    }

    void OnReleased(SelectExitEventArgs args)
    {
        m_IsHeld = false;
        m_ActivateInput = null;
    }

    void Update()
    {
        if (!m_IsHeld || m_ActivateInput == null)
            return;

        // อ่านค่าไก
        m_ActivateInput.TryReadValue(out float value);
        bool triggerDown = value >= triggerThreshold;

        // ตรวจ "ขอบขาขึ้น" = กดลงครั้งใหม่ (กดค้างไม่นับซ้ำ)
        if (triggerDown && !m_TriggerWasDown)
            Toggle();

        m_TriggerWasDown = triggerDown;
    }

    void Toggle()
    {
        m_IsOn = !m_IsOn;

        if (controlledLights != null)
        {
            foreach (var light in controlledLights)
            {
                if (light != null)
                    light.enabled = m_IsOn;
            }
        }

        if (clickSound != null)
            AudioSource.PlayClipAtPoint(clickSound, transform.position, clickVolume);
    }
}
