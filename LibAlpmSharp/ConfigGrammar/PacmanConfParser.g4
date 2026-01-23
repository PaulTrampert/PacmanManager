parser grammar PacmanConfParser;
options { tokenVocab=PacmanConfLexer; }

pacmanConf: sectionContent? section* ;

eol: (NEWLINE | STRING_NEWLINE | EOF) ;

section
        : sectionHeader sectionContent?
        ;
        
sectionHeader: LBRACKET (OPTIONS | REPO_ID) RBRACKET eol ;

sectionContent
    : (comment | setting | include)+
    ;

include
    : INCLUDE EQUALS settingValue eol
    ;
    
comment
    : COMMENT eol
    ;

setting
    : settingKey (EQUALS settingValues)? eol
    ;
    
settingValues
    : settingValue (STRING_WS settingValue)*
    ;
    
settingValue
    : string
    | quotedString
    ;
    
string
    : (STRING_CONTENT | STRING_ESCAPE_SEQUENCE)+
    ;
    
quotedString
    : (STRING_DQUOTE_OPEN dquoteStringContent* DQUOTE_CLOSE)
    | (STRING_SQUOTE_OPEN squoteStringContent* SQUOTE_CLOSE)
    ;
    
dquoteStringContent
    : (DQUOTE_ESCAPE_SEQUENCE | DQUOTE_CONTENT)
    ;
    
squoteStringContent
    : (SQUOTE_ESCAPE_SEQUENCE | SQUOTE_CONTENT)
    ;

settingKey
    : ROOTDIR
    | DBPATH
    | CACHEDIR
    | LOGFILE
    | GPGDIR
    | HOOKDIR
    | HOLDPKG
    | XFERCOMMAND
    | CLEANMETHOD
    | ARCHITECTURE
    | IGNOREPKG
    | IGNOREGROUP
    | NOUPGRADE
    | NOEXTRACT
    | USESYSLOG
    | COLOR
    | NOPROGRESSBAR
    | CHECKSPACE
    | VERBOSEPKGLISTS
    | PARALLELDOWNLOADS
    | DOWNLOADUSER
    | DISABLEDOWNLOADTIMEOUT
    | DISABLESANDBOX
    | DISABLESANDBOXFILESYSTEM
    | DISABLESANDBOXSYSCALLS
    | SIGLEVEL
    | LOCALFILESIGLEVEL
    | REMOTEFILESIGLEVEL
    | CACHESERVER
    | SERVER
    | USAGE
    ;



