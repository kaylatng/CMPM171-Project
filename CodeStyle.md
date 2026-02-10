# CMPM 171 Team C# & Unity Code Style Guide

This document defines the coding standards all team members must follow when committing C# code.  
Our goal is consistent, and readable code that we can maintain across gameplay, networking, and UI systems.

This guide follows general C# standards, Unity development practices, and team conventions.

---

## 1. Indentation & Spacing

- Use **4 spaces** per indentation level. Do **not** use tabs.
- Always include spaces around operators:

```csharp
int damage = baseDamage + bonusDamage;
```

- Include a space after commas:

```csharp
Foo(x, y, z);
```

- Add blank lines between:
  - Field declarations  
  - Methods  
  - Logical code sections  

---

## 2. Naming Conventions

### Classes, Structs, Enums
Use **PascalCase**.

```csharp
CardManager
PlayerNetwork
GameManager
GamePhase
```

### Methods
Use **PascalCase**.

```csharp
DrawCard()
StartNewTurnServer()
ExecuteDrawServer()
```

### Variables & Private Fields
Use **camelCase**.

```csharp
cardId
playerData
handCardIds
```

### Booleans
Use descriptive prefixes.

```csharp
isReady
isFree
canDraw
```

### Constants
Use **ALL_CAPS**.

```csharp
MAX_HAND_SIZE
STARTING_HP
```

### Unity Script Files
- The filename **must match the class name**.
- Use meaningful endings:
  - `Manager` (GameManager, CardManager)
  - `UI` (GameManagerUI)
  - `Network` (PlayerNetwork)

---

## 3. Brace Style

We use **Allman style** braces:

```csharp
if (canDraw)
{
    AddCardToHand(cardId);
}
```

- Opening brace on a new line.
- Braces required even for single-line blocks.

---

## 4. Formula & Expression Formatting

Break long calculations across lines:

```csharp
float damage = baseDamage
               + (attackPower * multiplier)
               - armorReduction;
```

- Use parentheses to make intent clear.
- Avoid long, compact expressions.

---

## 5. Code Readability Standards

- Keep lines under **100 characters**.
- Comments should explain **why**, so code is clear to all viewing.

Good:

```csharp
// Prevent drawing if hand is already full.
if (handCardIds.Count >= MAX_HAND_SIZE) return;
```

Bad:

```csharp
// add 1 
data.Mana += 1;
```

- Group related code sections.
- Remove temporary debug logs for finalized code before final commits.

---

## 6. Unity-Specific Conventions

### Inspector Fields

Use:

```csharp
[SerializeField] private TextMeshProUGUI manaText;
```

Instead of public fields.

### Scene References

- Avoid `GameObject.Find()` inside `Update()`.
- Cache references in `Start()` or `OnNetworkSpawn()`.

### Separation of Responsibilities

| System | Responsibility |
|--------|----------------|
| GameManager | Controls game phases |
| PlayerNetwork | Handles player data and networking |
| CardManager / DeckManager | Card and deck logic |
| UI Scripts | Visual display only |

---

## 7. Networking Rules (Unity Netcode)

- Use `NetworkVariable<T>` for shared data.
- Server logic must check:

```csharp
if (!IsServer) return;
```

- Client-only UI should check `IsOwner`.
- Do not change authoritative game data on clients.

---

## 8. Language Standard Compliance

All code must follow C# standards supported by Unity:

- Prefer properties or methods over public fields.
- Use structs for simple data containers (e.g., PlayerData).
- Follow Unity lifecycle methods (`Start`, `Update`, `OnDestroy`).

---

## 9. Git & Project Hygiene

Do **not** commit Unity-generated folders:

```
Library/
Temp/
Logs/
UserSettings/
```

Commits should be small and focused.

Example good commit message:

```
Add player resource UI and hook PlayerNetwork updates
```

---

## 10. Summary

All committed code must follow these standards to ensure:

- Consistent formatting  
- Clear naming  
- Readable structure   
- Safe networking code  
- Easy and clean repository management  
