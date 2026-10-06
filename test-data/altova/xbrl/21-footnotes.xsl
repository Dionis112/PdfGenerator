<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
 xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
 xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:t="urn:altova-fixture:taxonomy"
 exclude-result-prefixes="xs a xbrli t">
 <xsl:output method="xml" encoding="utf-8" indent="yes"/>
 <xsl:template match="/xbrli:xbrl">
  <result>
   <xsl:for-each select="t:*">
    <xsl:variable name="notes" select="a:xbrl-footnotes(.)"/>
    <fact id="{@id}" count="{count($notes)}"><xsl:copy-of select="$notes"/></fact>
   </xsl:for-each>
  </result>
 </xsl:template>
</xsl:stylesheet>
