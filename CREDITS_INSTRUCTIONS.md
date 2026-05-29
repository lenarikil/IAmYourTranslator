# Language Credits Setup

After the normal credits roll, a language credits screen can appear with custom translator credits. Configure it via `metadata` in your language JSON.

## Fields

```json
"metadata": {
    "langAuthor": "your name here",
    "langCredits": "Line 1 of credits\nLine 2 of credits\\nMore lines...",
    "langCreditsHeader": "Translator Credits"
}
```

| Field | Required | Description |
|---|---|---|
| `langAuthor` | No | Author name(s). Used as **fallback** when `langCredits` is empty. |
| `langCredits` | No | Main text. Supports `\n` (escaped newline) for multi-line. |
| `langCreditsHeader` | No | Header/title text. Default: `"Translator Credits"`. |

### Fallback

If `langCredits` is empty/null but `langAuthor` exists, the credits will show `langAuthor`


If both are empty, no language credits appear.

## Image

Place a PNG or JPG in the language's `textures/` folder:

```
sample-languages/ru-RU/
├── textures/
│   └── langCredits.png   (or langCredits.jpg)
└── ru-RU.json
```

The image appears on the left side of the credits panel (230px wide).
