parser grammar PacmanConfParser;
options { tokenVocab=PacmanConfLexer; }

pacmanConf
    : ((comment (eol section)*) | (eol* section (eol section)*))? eol? EOF
    ;

eol: (NEWLINE | STRING_NEWLINE) ;

section
        : sectionHeader (eol sectionContent)?
        ;
        
sectionHeader: LBRACKET (OPTIONS | REPO_ID) RBRACKET ;

sectionContent
    : (comment | setting | include) (eol (comment | setting | include))*
    ;

include
    : INCLUDE EQUALS STRING_WS? settingValue STRING_WS?
    ;
    
comment
    : COMMENT (eol COMMENT)*
    ;

setting
    : settingKey (EQUALS STRING_WS? settingValues)? STRING_WS?
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



