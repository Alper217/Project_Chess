import os
import xml.etree.ElementTree as ET
import glob

# Path to reports
path = os.path.expandvars(r'%LOCALAPPDATA%Low\DefaultCompany\Project_Chess\BotVsBotReports\Report_*.xml')
files = glob.glob(path)
files.sort(key=os.path.getmtime, reverse=True)
files = files[:10]  # Latest 10

summary = []

for f in files:
    try:
        tree = ET.parse(f)
        root = tree.getroot()
        
        game_id = root.find('GameID').text
        winner_id = root.find('Result/WinnerID').text
        
        p1_pawns = [p.get('name') for p in root.findall('Player[@id="1"]/SelectedPawns/Pawn')]
        p2_pawns = [p.get('name') for p in root.findall('Player[@id="2"]/SelectedPawns/Pawn')]
        
        summary.append({
            'game_id': game_id,
            'winner': winner_id,
            'p1_pawns': p1_pawns,
            'p2_pawns': p2_pawns
        })
    except Exception as e:
        print(f"Error parsing {f}: {e}")

print("Summary of latest 10 matches:")
p1_wins = sum(1 for s in summary if s['winner'] == '1')
p2_wins = sum(1 for s in summary if s['winner'] == '2')

print(f"Player 1 Wins: {p1_wins}")
print(f"Player 2 Wins: {p2_wins}")
print(f"Win Rate P1: {p1_wins/len(summary)*100 if summary else 0}%")
print(f"Win Rate P2: {p2_wins/len(summary)*100 if summary else 0}%")

for s in summary:
    print(f"Game {s['game_id']}: Winner {s['winner']}")
    print(f"  P1 Pawns: {', '.join(s['p1_pawns'])}")
    print(f"  P2 Pawns: {', '.join(s['p2_pawns'])}")
