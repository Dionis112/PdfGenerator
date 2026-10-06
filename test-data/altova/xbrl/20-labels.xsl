<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
 xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
 xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:t="urn:altova-fixture:taxonomy"
 exclude-result-prefixes="xs a xbrli t">
 <xsl:output method="xml" encoding="utf-8" indent="yes"/>
 <xsl:template match="/probes">
  <xsl:variable name="taxonomy" select="string(resolve-uri(string(@taxonomy), base-uri(.)))"/>
  <result>
   <xsl:for-each select="concept">
    <xsl:variable name="labels" select="a:xbrl-labels(resolve-QName(string(@name), .), $taxonomy)"/>
    <concept name="{@name}" count="{count($labels)}"><xsl:copy-of select="$labels"/></concept>
   </xsl:for-each>
  </result>
 </xsl:template>
</xsl:stylesheet>
