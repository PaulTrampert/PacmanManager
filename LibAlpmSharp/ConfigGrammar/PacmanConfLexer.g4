lexer grammar PacmanConfLexer;


COMMENT: '#' ~[\r\n]* ;
WS: [ \t]+ -> skip ;

NEWLINE: [\r\n]+ ;

LBRACKET: '[' ;
RBRACKET: ']' ;

EQUALS: '=' -> pushMode(STRING) ;

// Keywords
// General Options
ROOTDIR: 'RootDir';
DBPATH: 'DBPath';
CACHEDIR: 'CacheDir';
HOOKDIR: 'HookDir';
GPGDIR: 'GPGDir';
LOGFILE: 'LogFile';
HOLDPKG: 'HoldPkg';
IGNOREPKG: 'IgnorePkg';
IGNOREGROUP: 'IgnoreGroup';
INCLUDE: 'Include';
ARCHITECTURE: 'Architecture';
XFERCOMMAND: 'XferCommand';
NOUPGRADE: 'NoUpgrade';
NOEXTRACT: 'NoExtract';
CLEANMETHOD: 'CleanMethod';
SIGLEVEL: 'SigLevel';
LOCALFILESIGLEVEL: 'LocalFileSigLevel';
REMOTEFILESIGLEVEL: 'RemoteFileSigLevel';
USESYSLOG: 'UseSyslog';
COLOR: 'Color';
NOPROGRESSBAR: 'NoProgressBar';
CHECKSPACE: 'CheckSpace';
VERBOSEPKGLISTS: 'VerbosePkgLists';
DISABLEDOWNLOADTIMEOUT: 'DisableDownloadTimeout';
PARALLELDOWNLOADS: 'ParallelDownloads';
DOWNLOADUSER: 'DownloadUser';
DISABLESANDBOX: 'DisableSandbox';
DISABLESANDBOXFILESYSTEM: 'DisableSandboxFilesystem';
DISABLESANDBOXSYSCALLS: 'DisableSandboxSyscalls';

// Repository Options
CACHESERVER: 'CacheServer';
SERVER: 'Server';
USAGE: 'Usage';

OPTIONS: 'options' ;

REPO_ID: [a-zA-Z0-9-_]+;

mode STRING;
STRING_WS : [ \t]+ ;
STRING_NEWLINE : [\r\n]+ -> popMode;
STRING_ESCAPE_SEQUENCE : '\\' [btnfr"'\\ ] ;
STRING_CONTENT : ~[ \t"'\r\n\\]+ ;
STRING_DQUOTE_OPEN : '"' -> pushMode(DQUOTE_STRING);
STRING_SQUOTE_OPEN : '\'' -> pushMode(SQUOTE_STRING);

mode DQUOTE_STRING;
DQUOTE_ESCAPE_SEQUENCE
    :   '\\' [btnfr"'\\]
    ;
    
DQUOTE_CONTENT
    :   ~["\\\r\n]+
    ;
    
DQUOTE_CLOSE
    :   '"' -> popMode
    ;
    
mode SQUOTE_STRING;
SQUOTE_ESCAPE_SEQUENCE
    :   '\\' [btnfr'"\\]
    ;
    
SQUOTE_CONTENT
    :   ~['\\\r\n]+
    ;
    
SQUOTE_CLOSE
    :   '\'' -> popMode
    ;