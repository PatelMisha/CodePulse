"use client";

import Editor from "@monaco-editor/react";

const LANGUAGES = ["python", "javascript", "java", "csharp"];

const DEFAULT_CODE: Record<string, string> = {
  python: `# Write your Python code here
def two_sum(nums, target):
    seen = {}
    for i, num in enumerate(nums):
        complement = target - num
        if complement in seen:
            return [seen[complement], i]
        seen[num] = i
    return []

print(two_sum([2, 7, 11, 15], 9))
`,
  javascript: `// Write your JavaScript code here
function twoSum(nums, target) {
  const seen = new Map();
  for (let i = 0; i < nums.length; i++) {
    const complement = target - nums[i];
    if (seen.has(complement)) return [seen.get(complement), i];
    seen.set(nums[i], i);
  }
  return [];
}

console.log(twoSum([2, 7, 11, 15], 9));
`,
  java: `// Write your Java code here
public class Solution {
    public static void main(String[] args) {
        int[] result = twoSum(new int[]{2, 7, 11, 15}, 9);
        System.out.println(result[0] + ", " + result[1]);
    }

    public static int[] twoSum(int[] nums, int target) {
        java.util.Map<Integer, Integer> seen = new java.util.HashMap<>();
        for (int i = 0; i < nums.length; i++) {
            int complement = target - nums[i];
            if (seen.containsKey(complement)) return new int[]{seen.get(complement), i};
            seen.put(nums[i], i);
        }
        return new int[]{};
    }
}
`,
  csharp: `// Write your C# code here
var nums = new[] { 2, 7, 11, 15 };
var result = TwoSum(nums, 9);
Console.WriteLine($"[{result[0]}, {result[1]}]");

int[] TwoSum(int[] nums, int target) {
    var seen = new Dictionary<int, int>();
    for (int i = 0; i < nums.Length; i++) {
        int complement = target - nums[i];
        if (seen.ContainsKey(complement)) return [seen[complement], i];
        seen[nums[i]] = i;
    }
    return [];
}
`,
};

interface Props {
  code: string;
  language: string;
  onCodeChange: (value: string) => void;
  onLanguageChange: (lang: string) => void;
  onRun: () => void;
  isRunning: boolean;
}

export default function CodeEditor({
  code, language, onCodeChange, onLanguageChange, onRun, isRunning,
}: Props) {
  const handleLanguageChange = (lang: string) => {
    onLanguageChange(lang);
    onCodeChange(DEFAULT_CODE[lang] ?? "");
  };

  return (
    <div className="flex flex-col h-full bg-[#1e1e1e] rounded-lg overflow-hidden border border-[#333]">
      {/* Toolbar */}
      <div className="flex items-center justify-between px-4 py-2 bg-[#252526] border-b border-[#333]">
        <div className="flex items-center gap-2">
          <span className="text-xs text-[#858585] uppercase tracking-widest">Language</span>
          <select
            value={language}
            onChange={(e) => handleLanguageChange(e.target.value)}
            className="bg-[#3c3c3c] text-[#ccc] text-sm px-2 py-1 rounded border border-[#555] outline-none cursor-pointer"
          >
            {LANGUAGES.map((l) => (
              <option key={l} value={l}>
                {l.charAt(0).toUpperCase() + l.slice(1)}
              </option>
            ))}
          </select>
        </div>
        <button
          onClick={onRun}
          disabled={isRunning}
          className="flex items-center gap-2 px-4 py-1.5 bg-[#0e7a0e] hover:bg-[#1a9e1a] disabled:bg-[#333] disabled:cursor-not-allowed text-white text-sm font-medium rounded transition-colors"
        >
          {isRunning ? (
            <>
              <span className="w-3 h-3 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Running...
            </>
          ) : (
            <>▶ Run</>
          )}
        </button>
      </div>

      {/* Monaco Editor */}
      <div className="flex-1">
        <Editor
          height="100%"
          language={language === "csharp" ? "csharp" : language}
          value={code}
          onChange={(val) => onCodeChange(val ?? "")}
          theme="vs-dark"
          options={{
            fontSize: 14,
            fontFamily: "'Fira Code', 'Cascadia Code', monospace",
            fontLigatures: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false,
            lineNumbers: "on",
            renderLineHighlight: "line",
            tabSize: 2,
            wordWrap: "on",
            padding: { top: 16 },
          }}
        />
      </div>
    </div>
  );
}
