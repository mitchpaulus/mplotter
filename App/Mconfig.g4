grammar Mconfig ;

file : record* EOF ;

record : sourceSelector trendSelector transform ;

sourceSelector
  : typeSelector # TypeSelectorInfo
  | absPathSelector # AbsPathSelectorInfo
  ;

typeSelector : 'type' STRING ;
absPathSelector : 'absPath' STRING ;

trendSelector
    : 're' STRING # RegexTrendSelector
    | 'name' STRING # NameTrendSelector
    ;

transform
    : 'unit' STRING # UnitTransform
    | 'convert' STRING STRING # ConvertTransform
    | 'rename' STRING STRING # RenameTransform
    | 'replace' STRING STRING # ReplaceTransform
    ;

// See pg. 78 of Definitive ANTLR Reference.
STRING : '"' (ESC|.)*? '"' ;
fragment ESC : '\\"'  | '\\\\' ;

WS : [ \t\r\n]+ -> skip ;
