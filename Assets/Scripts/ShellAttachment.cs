using UnityEngine;

[System.Serializable]
public class ShellAttachment
{
    [Header("חיבור פיזי")]
    [Tooltip("נקודה על הקונכייה שתתיישר ל-shellMountPoint של השחקן")]
    public Transform attachPoint;

    [Header("השפעה על השחקן (במצב Carried)")]
    [Tooltip("הזזת מרכז המסה של השחקן יחסית למרכז הבסיסי שלו")]
    public Vector2 carryComShift = new Vector2(0f, 0.2f);

    [Header("התנהגות ויזואלית")]
    [Tooltip("האם הקונכייה תתהפך עם השחקן (מראה). false = שומרת על כיוון מקורי")]
    public bool mirrorWithPlayerFlip = true;

    [Header("מצב Encapsulating (כשהשחקן בתוך הקונכייה)")]
    [Tooltip("איפה השחקן יושב יחסית למרכז הקונכייה כשהוא בפנים")]
    public Vector2 playerSeatOffset = Vector2.zero;
}
