# 🦀 ShellBound — סקירת קוד מקצועית ערב הגיימפליי
**תאריך:** 2026-05-27 · **מבקר:** Claude · **גרסה:** main @ `89db0ff` (ART SHORE)

מסמך זה הוא מעבר שיטתי על כל הסקריפטים תחת `Assets/Scripts/`, ממוין לפי קריטיות. הוא מיועד לקריאה בערב לפני הפלייטסט — קודם **חוסמי שחרור (Showstoppers)**, אחר כך **באגים אמיתיים**, ואז **שיפורי איכות והערות פוליש**.

---

## 0. סיכום מנהלים

הקוד יציב באופן כללי, מודולרי ועומד בעקרונות שהוגדרו במסמך התכנון. עם זאת — זוהו **3 סיכונים חוסמים** שעלולים לקרוס/לתקוע משחק במהלך הפלייטסט, **8 באגים אמיתיים** עם פוטנציאל לפגוע בחוויה, ועוד **15 הערות איכות**. החוסמים פתירים ב-15 דקות סך הכל ומומלץ לטפל בהם הערב.

| חומרה | כמות | זמן תיקון מצטבר משוער |
| --- | --- | --- |
| 🔴 חוסם | 3 | ~15 דק׳ |
| 🟠 באג ממשי | 8 | ~45 דק׳ |
| 🟡 איכות / פוליש | 15 | אופציונלי |

---

## 1. 🔴 חוסמים (Showstoppers) — לטפל לפני הפלייטסט

### 1.1 שילוב `PipeTeleporter` + `TunaCan` במצב `InUse` = פחית ננטשת ושחקן בלי שליטה

**מיקום:** [PipeTeleporter.cs:40-46](Assets/Scripts/PipeTeleporter.cs#L40-L46) · [TunaCan.cs:68-108](Assets/Scripts/ShellScripts/TunaCan.cs#L68-L108)

**הבעיה:** במצב `InUse` של `TunaCan`, הקוליידרים של השחקן מבוטלים (`col.enabled = false`) — לכן `OnTriggerEnter2D` של הצינור לא יורה כלל מצד השחקן. **אם** משתמש מתגלגל ישירות לתוך טריגר הצינור, אין השתגרות — והוא תקוע. **בעיה שנייה:** אם השתגרות תוגדר בעתיד על קוליידר הפחית בעצמו, השחקן יושתג אך הפחית לא תיגרר אחריו (היא לא צאצא של השחקן ב-`InUse`).

**תיקון מהיר (לפני הפלייטסט):** הוסף בדיקה ב-`PipeTeleporter.OnTriggerEnter2D` — אם הקוליידר שייך לפחית פעילה, החזר את השחקן ל-`OnBack` לפני ההשתגרות:

```csharp
private void OnTriggerEnter2D(Collider2D collision)
{
    if (!canWarp) return;
    if (collision.CompareTag("Player"))
    {
        StartCoroutine(WarpRoutine(collision.gameObject));
        return;
    }
    // Catch the case where the player is rolling inside a TunaCan
    TunaCan can = collision.GetComponent<TunaCan>();
    if (can != null && can.CurrentState == ShellState.InUse && can.playerInside != null)
    {
        can.OnDeactivate();
        StartCoroutine(WarpRoutine(can.playerInside.gameObject));
    }
}
```

**הערה:** `playerInside` ב-`Shell.cs` מוגדר כ-`protected` — צריך להעלות ל-`public` (או להוסיף `public PlayerController PlayerInside => playerInside;`).

---

### 1.2 שחרור `grabJoint` לא מבוטל כשהשחקן נכנס לצינור / מתגלגל בפחית

**מיקום:** [PlayerController.cs:113-123](Assets/Scripts/PlayerController.cs#L113-L123) · [PipeTeleporter.cs:60-71](Assets/Scripts/PipeTeleporter.cs#L60-L71)

**הבעיה:** `SetControlEnabled(false)` מבטל את ה-`PlayerController` והקלט, אבל ה-`FixedJoint2D` שמחבר את השחקן ל-`Movable` נשאר. תוצאה: אם השחקן אוחז בקופסה ונכנס לצינור, **הקופסה משותגרת איתו** דרך הג׳וינט — או גרוע יותר, המרחק המקסימלי של הג׳וינט יישבר בעת ה-`Lerp` המהיר וייצור התנגשויות פיזיקליות פראיות.

תרחיש דומה: השחקן אוחז בקופסה, ואז לוחץ Space עם `TunaCan` — מה שמכניס אותו לפחית. הקופסה תיגרר.

**תיקון:** ב-`SetControlEnabled(false)` ובתחילת `OnActivate` של פחיות מתגלגלות — קרא ל-`ReleaseGrab()`. הפוך אותה ל-`public` (כרגע `private`).

```csharp
public void SetControlEnabled(bool isEnabled)
{
    enabled = isEnabled;
    if (input != null) input.enabled = isEnabled;
    if (!isEnabled)
    {
        currentVelocityX = 0f;
        moveTimer = 0f;
        ReleaseGrab(); // Safety: never warp while still holding something
    }
}
```

---

### 1.3 רוטציית השחקן לא מתאפסת ביציאה מ-`TunaCan` על שיפוע

**מיקום:** [TunaCan.cs:205-212](Assets/Scripts/ShellScripts/TunaCan.cs#L205-L212) · [TunaCan.cs:110-137](Assets/Scripts/ShellScripts/TunaCan.cs#L110-L137)

**הבעיה:** ב-`SyncPlayerPosition` כל פריים: `playerInside.transform.rotation = Quaternion.FromToRotation(Vector3.up, playerInside.SurfaceNormal)`. כאשר השחקן יוצא מהפחית בלחיצת Space על שיפוע, **ה-`transform.rotation` שלו נשאר מסובב לפי השיפוע**. ה-`FreezeRotation` שב-`PlayerController.Awake` חוסם רק את הפיזיקה — לא דורס `transform.rotation` ידני.

תוצאה ויזואלית: הסרטן עומד מסובב 30° ומחליק כאילו רגליו תקועות באוויר עד שהוא זז ויפסע מחדש (אז `visualsRoot` יסתובב, אבל הגוף הראשי של השחקן יישאר נטוי).

**תיקון:** בסוף `OnDeactivate` של `TunaCan` הוסף:

```csharp
if (playerInside != null)
{
    playerInside.transform.rotation = Quaternion.identity;
    RestorePlayerPhysics();
    // ... existing code
}
```

(וגם ב-`CheckThrowInput` לפני `OnThrow` — לטיפול ביציאה דרך זריקה.)

---

## 2. 🟠 באגים אמיתיים — לטפל בקרוב

### 2.1 `ArmorShell.OnPlayerCollisionEnter` שובר חפצים גם במגע איטי

**מיקום:** [ArmorShell.cs:105-134](Assets/Scripts/ShellScripts/ArmorShell.cs#L105-L134)

`OnPlayerCollisionEnter` יורה בכל התנגשות במצב `InUse` — כולל כשהשחקן פשוט נוגע בקלות בקופסה שבירה מלמעלה (`normal.y > 0.5f`). אין בדיקת `relativeVelocity` או `impulse`.

**תיקון:**

```csharp
if (contact.normal.y > 0.5f && collision.relativeVelocity.y < -3f)
{
    // ...
}
```

(הסף `-3` שלילי כי הקרב מגיע מלמעלה.)

---

### 2.2 `Physics2D.IgnoreCollision` לא מנוקה כשמחליפים קונכייה

**מיקום:** [Shell.cs:65-70](Assets/Scripts/ShellScripts/Shell.cs#L65-L70)

ב-`OnCollect` מופעלת `IgnoreCollision(playerCollider, shellCollider, true)`. הזוג הזה **לעולם לא מתאפס**. אחרי שזורקים פחית A, מרימים פחית B, וזורקים גם אותה — השחקן מסוגל לעבור דרך פחית A על הקרקע כי `IgnoreCollision` עדיין פעיל. אם משחק רב-קונכיות מתוכנן, זה ייצר תחושה של "שכבות רוח".

**תיקון:** ב-`OnDetach` (קוראים אליו אחרי `AutoLandRoutine`) — איפוס:

```csharp
if (playerInside != null) // not yet nulled
{
    var playerCol = playerInside.GetComponent<Collider2D>();
    if (playerCol != null && shellCollider != null)
        Physics2D.IgnoreCollision(playerCol, shellCollider, false);
}
```

בפועל, `playerInside` כבר `null` ב-`OnDetach`. הפתרון: שמור reference פרטי `Collider2D lastPlayerCollider` ושחרר אותו ב-`OnDetach`.

---

### 2.3 דחיפת קונכיות לא נגישות במצב טריגר

**מיקום:** [Shell.cs:188-191](Assets/Scripts/ShellScripts/Shell.cs#L188-L191)

ב-`OnDetach`: `rb.bodyType = Kinematic` + `shellCollider.isTrigger = true`. אם הקונכייה נופלת על משטח שטוח מסוים זה תקין — אבל אם היא נוחתת בזווית (למשל מתחת לקופסה), היא יכולה **לרחף באוויר** כי במצב Kinematic אין לה כובד. הזריקה מסתיימת ב-`AutoLandRoutine` כשהמהירות יורדת מתחת ל-0.1 — אבל אם הקונכייה נתקעה בקיר ועצרה בעודה באוויר, הטיימר של 2 שניות שולח `OnDetach` ומקבעת אותה ברחיפה.

**תיקון:** בסיום `AutoLandRoutine`, לפני `OnDetach`, ירה Raycast קצר כלפי מטה — אם אין קרקע במרחק <1m, חכה עוד 0.5s.

---

### 2.4 `SprayCan.pendingDash` לא מתבטל אם הקונכייה נזרקת בין `OnActivate` ל-`FixedUpdate`

**מיקום:** [SprayCan.cs:51-65](Assets/Scripts/ShellScripts/SprayCan.cs#L51-L65) · [SprayCan.cs:84-91](Assets/Scripts/ShellScripts/SprayCan.cs#L84-L91)

`pendingDash` מסומן כ-`true` בקורוטינה ומוחל ב-`FixedUpdate`. `ResetDashState` (שנקרא ב-`OnThrow`/`OnDetach`) לא מאפס את `pendingDash`. תרחיש קצה: לחיצה על Space ואז E בפריים הבא — הקורוטינה תיעצר, אבל `FixedUpdate` הבא עדיין יחיל את ה-`AddForce` על שחקן שכבר בלי קונכייה. כוח רנדומלי.

**תיקון:**

```csharp
private void ResetDashState()
{
    if (dashCoroutine != null) StopCoroutine(dashCoroutine);
    dashCoroutine = null;
    isDashing = false;
    pendingDash = false; // <-- ADD THIS
}
```

---

### 2.5 `TryInteractShell` משנה את `Physics2D.queriesHitTriggers` גלובלית

**מיקום:** [PlayerController.cs:355-368](Assets/Scripts/PlayerController.cs#L355-L368)

זה משתנה גלובלי. בין השורה `Physics2D.queriesHitTriggers = true` לשורה `= originalHitTriggers`, כל סקריפט אחר שמריץ `Physics2D.OverlapXXX` באותו פריים יקבל תוצאות שונות מהמצופה. כרגע אין שני סקריפטים שקוראים בו-זמנית — אבל זו פצצה שמחכה.

**תיקון:** השתמש ב-`ContactFilter2D` עם `useTriggers = true`, או החלף לקוליידרים שאינם טריגרים כשהקונכייה על הקרקע (וזה מבטל את הצורך לגמרי).

---

### 2.6 `Update()` ריקה ב-`TunaCan`

**מיקום:** [TunaCan.cs:33-37](Assets/Scripts/ShellScripts/TunaCan.cs#L33-L37)

מתודה ריקה גורמת ל-Unity לקרוא אליה כל פריים מצד native code. עלות זניחה אבל מפריעה לפרופיילר. **מחק.**

---

### 2.7 `Parallax.cs` קורסת אם המצלמה הראשית לא קיימת ב-`Start`

**מיקום:** [Parallax.cs:12-20](Assets/Scripts/Parallax.cs#L12-L20)

```csharp
if (cam == null) cam = Camera.main;
// ...
camStartPosition = cam.transform.position; // NRE if Camera.main was also null
```

**תיקון:**

```csharp
if (cam == null) cam = Camera.main;
if (cam == null) { enabled = false; return; }
```

---

### 2.8 `CrabController.cs` (סקריפט ישן של עמיר) כפול עם `PlayerController`

**מיקום:** [CrabController.cs](Assets/Scripts/Amir%20Scripts/CrabController.cs)

שני הסקריפטים דורשים `PlayerInputHandler`, שניהם קוראים ל-`MoveValue.x`, ושניהם כותבים ל-`rb`. אם שניהם מצורפים בטעות לאותו GameObject — בלגן פיזיקלי. `CrabController` גם מגדיר `gravityScale = 0` ב-`Awake`, מה שיגרום למצב הראשי לעבוד בלי כבידה.

**תיקון:** העבר את `Amir Scripts/` ל-`_Backups/` או הוסף `#if false` סביב המחלקה. או לפחות `[DisallowMultipleComponent]` עם הערה ברורה שזה לא בשימוש.

---

## 3. 🟡 איכות, פוליש והערות עיצוב

### 3.1 קידוד `AudioManager.cs` שבור

[AudioManager.cs](Assets/Scripts/AudioManager.cs) שמור כ-Windows-1252 — הערות עברית מופיעות כג׳יבריש (`�����`). הקוד עובד, אבל בלתי קריא. שמור מחדש כ-UTF-8 ב-VS Code (Ctrl+Shift+P → "Change File Encoding") — או החלף הערות ל-English לפי [[feedback_code_comments_language]].

### 3.2 שירותי שמע ב-`AudioManager`

- `PlaySFX` משתמש ב-`musicSource.PlayOneShot` — SFX מתערבב עם המוזיקה ומקצץ אותה אם נטען ביחד. צור שלישי `sfxSource` נפרד.
- `fadeCoroutine` אף פעם לא מאופס ל-`null` בסיום `FadeOutMusic`. תוסיף בסוף הקורוטינה: `instance.fadeCoroutine = null;`.

### 3.3 `Debug.Log` שופע ב-`PlayerController.TryInteractShell`

ה-`Log` שמתאר "כל אובייקט שנמצא בקרבת מקום" יורה בכל לחיצת E — גם בלי קונכיות. בפלייטסט הקונסול יתפוצץ. עטוף ב-`#if UNITY_EDITOR` או הוסף שדה `bool verboseLogs`.

### 3.4 `Movable.cs` מבצע `FixedUpdate` לכל קופסה במשחק

כל `Movable` בודק `currentMaxPushMass` 50 פעמים בשנייה. עם 20 קופסאות בשלב = 1000 קריאות/שניה. זול בפועל, אבל אם יש 200 קופסאות אובייקטים בעולם — שווה לעבור ל-event-driven (השחקן יודיע לקופסאות כשמתחלפת היכולת).

### 3.5 שמירה כפולה של `currentVelocityX` ב-`HandleMovement`

`currentVelocityX = rb.linearVelocity.x` ב-`InUse` (שורה 183) — נכון מבחינת הלוגיקה, אבל הערך נכתב מחדש בכל פריים פיזיקה. אם השחקן ב-`InUse` במצב `Kinematic` (כמו ב-`TunaCan.InUse`), `rb.linearVelocity` הוא של Kinematic body שלא משתמש בו — הערך יהיה 0 בכל פריים, ולאחר היציאה השחקן יתחיל מ-0 גם אם הפחית התגלגלה במהירות גבוהה. **זה כנראה הכוונה** — אבל וודא שזו ההתנהגות הרצויה.

### 3.6 `OnDrawGizmos` של `PlayerController` נטען תמיד

יוצא לעיבוד בעורך גם כשהאובייקט לא נבחר. עם 50 ערכי Gizmo בכל פריים בעורך זה בסדר. מומלץ להוסיף toggle.

### 3.7 `BaseShell.AutoLandRoutine` מחכה 0.5 + 0.2 שניות בנפרד

[Shell.cs:155-160](Assets/Scripts/ShellScripts/Shell.cs#L155-L160) — שני `WaitForSeconds` רצופים. החלף ל-`yield return new WaitForSeconds(0.7f);`.

### 3.8 `BaseShell.OnDetach` קורא ל-`shellCollider.sharedMaterial = null`

מאפס את חומר הפיזיקה בכל ירידה לקרקע. אם הוגדר `PhysicsMaterial2D` עם קפיצות (לדוגמה הקפיץ), הוא יאופס. **שמור reference לחומר המקורי ב-`Awake` ושחזר אותו ב-`OnDetach`**:

```csharp
private PhysicsMaterial2D originalMaterial;

protected virtual void Awake()
{
    // ...
    originalMaterial = shellCollider.sharedMaterial;
}

public virtual void OnDetach()
{
    // ...
    shellCollider.sharedMaterial = originalMaterial; // instead of null
}
```

### 3.9 `TunaCan.lastJumpTime` נשמר בין הפעלות

אם מתגלגלים, יוצאים, נכנסים שוב — הקפיצה הראשונה אחרי הכניסה החוזרת תהיה בקולדאון. אפס ב-`OnDeactivate`: `lastJumpTime = -100f`.

### 3.10 `SpringShell` לא מציין `OnDeactivate` — הקובץ ריק כצפוי, אבל ה-base כן מצהיר `abstract`

לא — בדקתי, ב-`Shell.cs:128` הוא `public abstract void OnDeactivate();`. הקפיץ ממש *חייב* לממש אותו. נכון. רק להזכיר: אם תוסיף שדה "טיפוס יכולת" עם `[CanBeNull]` או דומה בעתיד — תרצה שמצב toggle לא יבלבל אותו.

### 3.11 `PipeTeleporter.shrinkPlayer` יחד עם flip שמאל/ימין

[PipeTeleporter.cs:128-129](Assets/Scripts/PipeTeleporter.cs#L128-L129) — `localScale = originalScale` משחזר את הסקייל המקורי **כולל הסימן השלילי של flip**. תקין, אבל אם השחקן מתהפך באמצע ה-`Lerp` (אין סיכוי כי הקלט מבוטל) זה לא יקרה. ✔

### 3.12 `Shell.OnDetach` לא מאפס את `playerInside`

הוא כן — שורה 179: `playerInside = null;`. ✔ — מצב טוב.

### 3.13 הצעת ארכיטקטורה: `currentShell` כ-`public` בעיתי

`PlayerController.currentShell` הוא public כדי שסקריפטי קונכייה יוכלו לקרוא לו. בעתיד עדיף לשמור property עם setter פרטי שמבצע cleanup אוטומטי, כדי שאף אחד מבחוץ לא ידרוס בטעות. כרגע ההערה ב-[PlayerController.cs:344](Assets/Scripts/PlayerController.cs#L344) רומזת שזו תקלה ידועה — תיקון נקי יהיה:

```csharp
[SerializeField] private Shell _currentShell;
public Shell currentShell
{
    get => _currentShell;
    set
    {
        if (_currentShell != null && _currentShell != value) /* cleanup */;
        _currentShell = value;
    }
}
```

### 3.14 `[RequireComponent(typeof(Collider2D))]` ב-`Shell` תופס רק את הראשון

אם לקונכייה יש כמה Colliders (למשל Box + Circle לדיוק יותר טוב), `GetComponent<Collider2D>()` יחזיר רק את הראשון, וכל הלוגיקה של `enabled`, `isTrigger`, `IgnoreCollision` תפעל רק עליו. השדה `public Collider2D shellCollider` מאפשר להגדיר במפורש — אבל בדוק שכל ה-`canTopSprite`/`canSideSprite` משתמשים באותו קוליידר.

### 3.15 משימה פתוחה מהמסמך: `PipeTeleporter` × נעילת קלט — סגור! ✔

ה-`TODO` במסמך מציין שצריך לנעול קלט. בדקתי — `SetControlEnabled(false)` כן נקראת. שווה לעדכן את [`ShellBound_Docs.md`](Assets/Scripts/ShellBound_Docs.md) ולמחוק את הסעיף משימות פתוחות (סעיף 5).

---

## 4. סדר העדיפויות לערב

```
1. 1.1 — תיקון פחית+צינור            [5 דק']
2. 1.2 — ReleaseGrab ב-SetControlEnabled [3 דק']
3. 1.3 — איפוס רוטציית שחקן ביציאה מהפחית [3 דק']
4. 2.1 — סף מהירות לריסוק שריון      [3 דק']
5. 2.4 — pendingDash ב-ResetDashState [1 דק']
6. 2.7 — guard ל-Camera.main בParallax [1 דק']
```

הכל ביחד **~15-20 דקות**. אחר כך אפשר ללכת לישון רגוע.

---

## 5. בדיקות עשן (Smoke Tests) לפני הפלייטסט

תרחישים שכדאי לעבור עליהם פעם אחת ידנית ב-Editor:

| # | תרחיש | תוצאה צפויה |
| - | --- | --- |
| 1 | הרים שריון → דחוף קופסה כבדה → זרוק שריון → דחוף קופסה רגילה | מעבר חלק בין יכולת דחיפה |
| 2 | הרים פחית → Space → התגלגל → Space שוב | יציאה חלקה למצב נשיאה |
| 3 | הרים פחית → Space → התגלגל → E (זרוק תוך כדי) | שחקן יוצא בלי תקיעה |
| 4 | הרים פחית → התגלגל לתוך צינור | (לאחר תיקון 1.1) המעבר עובד |
| 5 | החזק Ctrl על קופסה → היכנס לצינור | (לאחר תיקון 1.2) שחרור אוטומטי |
| 6 | הרים שריון → קפוץ על קופסה שבירה ממש מקרוב | (לאחר תיקון 2.1) לא מתפוצץ במגע איטי |
| 7 | זרוק קונכייה לפינה צרה → המתן 2 שניות | קונכייה מתייצבת על הקרקע, לא רחפה |
| 8 | פתח קונסול → לחץ E ליד שום דבר 20 פעמים | ספאם של Log — לפלייטסט עצמו אפשר לחיות איתו |
| 9 | הפעל סצנה ללא Main Camera | (לאחר תיקון 2.7) שום שגיאה |
| 10 | בדוק שכל הקונכיות בשלב יושבות על Layer `Shells` ולא Default | מאפשר ל-`shellLayer` ב-`PlayerController` למצוא אותן |

---

## 6. דברים שעובדים יפה (חבל לא להגיד)

- **ארכיטקטורת `Shell` בסיס** — מודולרית, נקייה, קלה להרחבה. `MovementSpeedMultiplier` ו-`ActiveAnchorOffset` כ-virtual properties = פתרון אלגנטי.
- **`HandleGroundCheck` עם `RaycastAll`** — הפתרון של "אל תסנן לפי שכבה, סנן בקוד" עובד נהדר לסצנות גמישות.
- **חוקיות שלבי תאוצה ב-`HandleMovement`** — `accelerationDelay` + `accelerationRate` + `decelerationRate` יוצרים תחושה מצוינת. שמור.
- **ה-Gizmos** — קל לראות מה השחקן יודע על העולם (אזור איסוף, חיישן קרקע, ידית תפיסה). שמור גם זה.

---

## 7. דברים שצריך לעקוב אחריהם בפלייטסט עצמו

לא לתקן — רק לרשום לעצמך לתשומת לב של הבודקים:

1. **תחושה של איסוף ידני (E):** האם הראדיוס `interactRadius=1.5` הגיוני? אולי גדול מדי?
2. **כובד הפחית בזמן גלגול:** האם `torqueForce=25` מתאים? יורד מהר מדי במורד שיפוע?
3. **קולדאון `dashCooldown=1.5`:** האם זה ארוך מדי כשמשרשרים?
4. **השריון `speedMultiplier=0.5`:** מספיק "כבד" או יותר מדי איטי?
5. **`SpringShell.jumpForce=15`:** מגיע לפלטפורמות שצריך, או שצריך לתמרן?

---

*סוף המסמך. שיהיה פלייטסט מוצלח 🦀*
