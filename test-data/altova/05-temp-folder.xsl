<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:ext="http://www.altova.com/xslt-extensions" exclude-result-prefixes="ext">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/environment-probe">
    <xsl:variable name="first" select="ext:get-temp-folder()"/>
    <xsl:variable name="second" select="ext:get-temp-folder()"/>
    <result probe="{@name}" nonempty="{string-length($first) gt 0}" stable="{$first eq $second}">
      <path><xsl:value-of select="$first"/></path>
      <codepoints><xsl:value-of select="string-to-codepoints($first)" separator=" "/></codepoints>
    </result>
  </xsl:template>
</xsl:stylesheet>
